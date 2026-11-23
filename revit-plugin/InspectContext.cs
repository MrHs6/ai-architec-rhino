using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ArchiTec.Revit;

[Transaction(TransactionMode.ReadOnly)]
public sealed class InspectContext : IExternalCommand
{
    public Result Execute(ExternalCommandData command, ref string message, ElementSet elements)
    {
        var uiDocument = command.Application.ActiveUIDocument;
        if (uiDocument is null)
        {
            message = "Open a Revit document first.";
            return Result.Cancelled;
        }

        var document = uiDocument.Document;
        var selection = uiDocument.Selection.GetElementIds();
        var details = selection.OrderBy(id => id.Value).Take(20)
            .Select(document.GetElement)
            .Where(element => element != null)
            .Select(element => $"{element.Category?.Name ?? "Element"} · {element.Name} · {element.UniqueId}");

        new TaskDialog("AI Archi-Tec")
        {
            MainInstruction = document.Title,
            MainContent = $"View: {uiDocument.ActiveView.Name}\nSelected elements: {selection.Count}",
            ExpandedContent = string.Join("\n", details),
            FooterText = selection.Count > 20 ? "Showing the first 20 selected elements." : ""
        }.Show();
        return Result.Succeeded;
    }
}
