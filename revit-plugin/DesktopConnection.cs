using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ArchiTec.Revit;

public sealed class DesktopConnection : IExternalApplication, IExternalEventHandler
{
    private readonly CancellationTokenSource stop = new();
    private readonly Dictionary<string, Session> sessions = new(StringComparer.OrdinalIgnoreCase);
    private static string DocumentKey(Document doc) => doc.CreationGUID + "|" + (doc.PathName.Length == 0 ? doc.Title : doc.PathName);
    private sealed class Session
    {
        public readonly string Id = Guid.NewGuid().ToString();
        public readonly Dictionary<Guid, (string Payload, object Receipt)> Completed = new();
    }
    private sealed record Request(JsonElement Body, TaskCompletionSource<string> Reply);
    private ExternalEvent signal = null!;
    private Request? pending;

    public Result OnStartup(UIControlledApplication application)
    {
        signal = ExternalEvent.Create(this);
        application.ControlledApplication.DocumentClosing += ForgetDocument;
        _ = Task.Run(Listen);
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        application.ControlledApplication.DocumentClosing -= ForgetDocument;
        stop.Cancel();
        signal.Dispose();
        return Result.Succeeded;
    }

    public string GetName() => "AI Archi-Tec Desktop";
    private void ForgetDocument(object? sender, Autodesk.Revit.DB.Events.DocumentClosingEventArgs args) =>
        sessions.Remove(DocumentKey(args.Document));

    private async Task Listen()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream("AIArchiTec.Revit." + Environment.ProcessId,
                    PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stop.Token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(40));
                using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
                var line = await reader.ReadLineAsync(timeout.Token);
                if (line == null || line.Length > 65536) continue;
                var request = new Request(JsonSerializer.Deserialize<JsonElement>(line), new(TaskCreationOptions.RunContinuationsAsynchronously));
                Interlocked.Exchange(ref pending, request);
                try
                {
                    if (signal.Raise() == ExternalEventRequest.Denied) throw new IOException("Revit is busy.");
                    await writer.WriteLineAsync(await request.Reply.Task.WaitAsync(timeout.Token));
                }
                finally { Interlocked.CompareExchange(ref pending, null, request); }
            }
            catch (Exception) when (!stop.IsCancellationRequested) { await Task.Delay(100); }
            catch (OperationCanceledException) { return; }
        }
    }

    public void Execute(UIApplication app)
    {
        var request = Interlocked.Exchange(ref pending, null);
        if (request == null) return;
        try { request.Reply.TrySetResult(JsonSerializer.Serialize(Run(app, request.Body))); }
        catch (Exception ex) { request.Reply.TrySetResult(JsonSerializer.Serialize(new { error = ex.Message })); }
    }

    private object Run(UIApplication app, JsonElement body)
    {
        var ui = app.ActiveUIDocument ?? throw new InvalidOperationException("Open a Revit project.");
        var doc = ui.Document;
        var documentKey = DocumentKey(doc);
        if (!sessions.TryGetValue(documentKey, out var session)) sessions.Add(documentKey, session = new Session());
        var tool = body.GetProperty("name").GetString();
        if (tool == "get_context")
            return new { document_id = session.Id, document = doc.Title, view = ui.ActiveView.Name,
                levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().Select(l => new { id = l.UniqueId, name = l.Name }).Take(100).ToArray(),
                selected = ui.Selection.GetElementIds().Take(50).Select(id => doc.GetElement(id)).Select(e => new { id = e.UniqueId, category = e.Category?.Name, name = e.Name }).ToArray() };
        var args = body.GetProperty("arguments");
        if (args.GetProperty("document_id").GetString() != session.Id) throw new InvalidOperationException("Active document changed. Refresh context.");
        Wall wall;
        if (tool == "inspect_wall") return WallInfo((Wall)doc.GetElement(args.GetProperty("wall_id").GetString()));
        if (tool != "create_wall" && tool != "set_wall_height" && tool != "create_model_line" && tool != "save_project") throw new ArgumentException("Unknown operation.");
        var key = args.GetProperty("request_id").GetGuid();
        var payload = body.GetRawText();
        if (session.Completed.TryGetValue(key, out var previous))
            return previous.Payload == payload ? previous.Receipt : throw new ArgumentException("Request ID already used.");
        double Feet(string field) => UnitUtils.ConvertToInternalUnits(args.GetProperty(field).GetDouble(), UnitTypeId.Meters);
        if(tool=="save_project"){
            if(string.IsNullOrEmpty(doc.PathName) || doc.IsReadOnly || doc.IsWorkshared)throw new InvalidOperationException("Choose a named writable local project.");
            doc.Save();
            var saved=new {path=doc.PathName};session.Completed.Add(key,(payload,saved));return saved;
        }
        if(tool=="create_model_line"){
            var level=doc.GetElement(args.GetProperty("level_id").GetString()) as Level ?? throw new ArgumentException("Level not found.");
            var start=new XYZ(Feet("x1_m"),Feet("y1_m"),level.Elevation);
            var end=new XYZ(Feet("x2_m"),Feet("y2_m"),level.Elevation);
            using var lineTx=new Transaction(doc,"AI Archi-Tec · Model line");lineTx.Start();
            var plane=SketchPlane.Create(doc,Plane.CreateByNormalAndOrigin(XYZ.BasisZ,start));
            var curve=doc.Create.NewModelCurve(Line.CreateBound(start,end),plane);
            var receipt=new {element_id=curve.UniqueId};
            if(lineTx.Commit()!=TransactionStatus.Committed)throw new InvalidOperationException("Line transaction failed.");
            session.Completed.Add(key,(payload,receipt));return receipt;
        }
        var height = Feet("height_m");
        if (height <= 0 || height > 10000) throw new ArgumentOutOfRangeException("height_m");
        using var tx = new Transaction(doc, "AI Archi-Tec · Wall");
        tx.Start();
        if (tool == "create_wall")
        {
            var level = (Level)doc.GetElement(args.GetProperty("level_id").GetString());
            var line = Line.CreateBound(new XYZ(Feet("x1_m"), Feet("y1_m"), level.Elevation), new XYZ(Feet("x2_m"), Feet("y2_m"), level.Elevation));
            var type = doc.GetDefaultElementTypeId(ElementTypeGroup.WallType);
            wall = Wall.Create(doc, line, type, level.Id, height, 0, false, false);
        }
        else
        {
            wall = (Wall)doc.GetElement(args.GetProperty("wall_id").GetString());
            var p = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
            if (p == null || p.IsReadOnly || !p.Set(height)) throw new ArgumentException("Wall height is constrained.");
        }
        doc.Regenerate();
        var info = WallInfo(wall);
        if (tx.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Wall transaction did not commit.");
        session.Completed.Add(key, (payload, info));
        return info;
    }

    private static object WallInfo(Wall wall)
    {
        if (wall == null) throw new ArgumentException("Wall does not exist.");
        var curve = ((LocationCurve)wall.Location).Curve;
        double M(double x) => UnitUtils.ConvertFromInternalUnits(x, UnitTypeId.Meters);
        double[] Point(XYZ p) => new[] { M(p.X), M(p.Y), M(p.Z) };
        return new { wall_id = wall.UniqueId, type = wall.Name, height_m = M(wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble()),
            straight = curve is Line, start_m = Point(curve.GetEndPoint(0)), end_m = Point(curve.GetEndPoint(1)) };
    }
}
