using System.Reflection;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using ArchiTec.Core;
using ArchiTec.RhinoPlugin;
using ArchiTec.RhinoPlugin.Views;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using GH_IO.Serialization;
using Rhino;
using Rhino.Geometry;

namespace ArchiTec.RuntimeTests;

/// <summary>Run only in a disposable Rhino test document after the real MCP graph test.</summary>
public static class RuntimeAcceptance
{
    public static void Schedule(string outputDirectory,string planPath)
    {
        EventHandler? idle=null;
        idle=async (_,_)=>
        {
            var doc=RhinoDoc.ActiveDoc;if(doc is null||doc.UndoRecordingIsActive)return;
            RhinoApp.Idle-=idle;
            var passes=new List<string>();
            void Check(bool ok,string name){if(!ok)throw new Exception(name);passes.Add(name);RhinoApp.WriteLine("PASS "+name);}
            Directory.CreateDirectory(outputDirectory);
            try
            {
                var panel=ArchiTecPanel.Current??throw new Exception("Start the Archi-Tec panel first.");
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var view=(StudioView)typeof(ArchiTecPanel).GetField("view",flags)!.GetValue(panel)!;
                var providerField=typeof(ArchiTecPanel).GetField("provider",flags)!;
                var original=providerField.GetValue(panel);
                var savedIds=doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.AnyObject).Select(o=>o.Id).ToHashSet();
                var savedCount=savedIds.Count;
                var template=Wire.Read<ActionPlan>(File.ReadAllText(planPath));
                var recipe=GrasshopperPlan.Read(template.Actions.Single().Parameters) with{Name="API Parametric Box",DefinitionId=null,BakeNodes=[]};
                recipe=recipe with{Nodes=recipe.Nodes.Select(n=>n.Kind=="slider"?n with{Value=n.Id=="width"?1:n.Id=="depth"?2:3}:n).ToList()};
                ActionPlan Plan(GrasshopperDefinition r)=>new(){Summary="API Grasshopper acceptance",LengthUnit="document",Actions=[ModelAction.Make(GrasshopperPlan.Tool,r)]};
                using var mock=new MockPlanner(Plan(recipe));
                try
                {
                    providerField.SetValue(panel,mock);view.SelectApiRoute();
                    ((ComboBox)view.FindName("TargetSelector")).SelectedIndex=1;
                    view.SetWorkspace("generate",false);
                    Check(((TextBlock)view.FindName("WorkspaceHelp")).Text.Contains("Graph Undo is in Grasshopper"),"Grasshopper guidance survives workspace refresh");
                    view.SetCatalog([new("acceptance/mock","Acceptance mock (no network)",false)],"acceptance/mock","OpenAI-compatible / local");
                    var definitions=Instances.DocumentServer.DocumentCount;
                    view.Prompt="Create an editable Grasshopper definition, with no bake.";
                    await panel.SendAsync();
                    Check(mock.Request?.Context.Grasshopper is not null,"API planner receives actual Grasshopper context and component catalog");
                    Check(typeof(ArchiTecPanel).GetField("pending",flags)!.GetValue(panel) is not null,"API Send prepares a real Grasshopper plan");
                    Check(Instances.DocumentServer.DocumentCount==definitions&&doc.Objects.Count==savedCount,"API proposal does not commit a graph or bake before Apply");
                    panel.ApplyPending();
                    var graph=Instances.ActiveCanvas.Document;
                    Check(graph is not null&&graph.ObjectCount==7&&Instances.DocumentServer.DocumentCount==definitions+1,"API Apply creates a new definition with seven editable Grasshopper nodes");
                    Check(doc.Objects.Count==savedCount,"API graph remains parametric without an explicit bake");
                    var id=graph!.RuntimeID;
                    recipe=recipe with{DefinitionId=id,BakeNodes=["box"],Nodes=recipe.Nodes.Select(n=>n.Id=="width"?n with{Value=6}:n).ToList()};
                    mock.Next=Plan(recipe);view.Prompt="Change the box width to 6 document units and bake the box into Rhino.";
                    await panel.SendAsync();panel.ApplyPending();
                    Check(Instances.ActiveCanvas.Document.RuntimeID==id&&Instances.ActiveCanvas.Document.ObjectCount==7,"API follow-up replaces the same definition without duplicate nodes");
                    var added=doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.AnyObject).Where(o=>!savedIds.Contains(o.Id)).ToArray();
                    Check(added.Length==1,"API explicit bake creates one Rhino object");
                    var brep=added.Single().Geometry as Brep;
                    Check(brep is not null&&brep.IsValid&&brep.IsSolid&&Math.Abs(VolumeMassProperties.Compute(brep).Volume-36)<1e-6,"API bake is a valid closed 6 x 2 x 3 native solid with volume 36");
                    Check(savedIds.All(i=>doc.Objects.FindId(i) is not null),"API edits preserve the earlier MCP baked geometry");
                    graph.Undo();graph.NewSolution(true,GH_SolutionMode.Silent);
                    Check(graph.Objects.OfType<GH_NumberSlider>().Single(s=>s.NickName=="Width").CurrentValue==1,"Grasshopper Undo restores the earlier graph and slider value");
                    graph.Redo();graph.NewSolution(true,GH_SolutionMode.Silent);
                    Check(graph.Objects.OfType<GH_NumberSlider>().Single(s=>s.NickName=="Width").CurrentValue==6,"Grasshopper Redo restores the revised graph");
                    Check(doc.Undo(),"Rhino Undo is available independently for the baked solid");
                    if(doc.UndoRecordingIsActive)doc.EndUndoRecord(doc.CurrentUndoRecordSerialNumber);
                    Check(doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.AnyObject).Count(o=>!o.IsDeleted)==savedCount&&savedIds.All(i=>doc.Objects.FindId(i) is {} o&&!o.IsDeleted),"Rhino Undo removes only the most recent bake");
                    var beforeGraph=Instances.ActiveCanvas.Document;
                    var beforeSliders=beforeGraph.Objects.OfType<GH_NumberSlider>().Select(s=>s.CurrentValue).ToArray();
                    var invalid=recipe with{BakeNodes=["box","width"]};bool rejected=false;
                    try{using var prepared=GeometryEngine.Prepare(doc,Plan(invalid));GeometryEngine.Apply(doc,prepared);}catch(ArgumentException){rejected=true;}
                    Check(rejected&&doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.AnyObject).Count(o=>!o.IsDeleted)==savedCount,"A later non-geometric bake item rolls back the earlier baked solid");
                    Check(beforeGraph==Instances.ActiveCanvas.Document&&beforeGraph.Objects.OfType<GH_NumberSlider>().Select(s=>s.CurrentValue).SequenceEqual(beforeSliders),"Failed baking leaves the existing editable graph unchanged");
                    var archive=new GH_Archive();archive.AppendObject(graph,"Definition");
                    Check(archive.WriteToFile(Path.Combine(outputDirectory,"API-Parametric-Box.gh"),false,false),"Editable .gh example is exported successfully");
                    using var reopened=new GH_Document();var read=new GH_Archive();
                    Check(read.ReadFromFile(Path.Combine(outputDirectory,"API-Parametric-Box.gh"))&&read.ExtractObject(reopened,"Definition")&&reopened.ObjectCount==7,"Exported .gh file reopens with all seven native nodes");
                    Check(McpCatalog.Version=="0.6.0-beta.6","Runtime test uses the installed final candidate version");
                }
                finally{panel.ClearPreview();providerField.SetValue(panel,original);if(original is null)view.SetOffline();}
                File.WriteAllText(Path.Combine(outputDirectory,"runtime-api-acceptance.json"),JsonSerializer.Serialize(new{status="passed",passes,mock_api=true,live_paid_provider_tested=false},Wire.Json));
            }
            catch(Exception e){File.WriteAllText(Path.Combine(outputDirectory,"runtime-api-acceptance.json"),JsonSerializer.Serialize(new{status="failed",passes,error=e.ToString()},Wire.Json));}
        };
        RhinoApp.Idle+=idle;
    }
    private sealed class MockPlanner(ActionPlan next):IPlannerClient
    {
        public ActionPlan Next=next;public PlanRequest? Request;
        public Task<ModelCatalog> Models(CancellationToken cancel)=>Task.FromResult(new ModelCatalog([new("acceptance/mock","Acceptance",false)]));
        public Task<PlanResponse> Plan(PlanRequest request,CancellationToken cancel){Request=request;return Task.FromResult(new PlanResponse("Review the editable Grasshopper definition.",Next,request.Model));}
        public void Dispose(){}
    }
}
