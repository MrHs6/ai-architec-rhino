using System.Globalization;
using System.Text.RegularExpressions;

namespace ArchiTec.Core;

/// <summary>Explicit offline examples only. This is not a substitute for an AI model.</summary>
public static class LocalPlanner
{
    public static ActionPlan Plan(string prompt, ModelContext context)
    {
        var t = Normalize(prompt).ToLowerInvariant();
        if(Regex.IsMatch(t,@"\bbuilding\b|ساختمان"))
        {
            var dims=Regex.Match(t,@"(\d+(?:\.\d+)?)\s*[x×*]\s*(\d+(?:\.\d+)?)");
            var levels=Regex.Match(t,@"(\d+)\s*(?:floors?|storeys?|stories|طبقه)");
            var height=Regex.Match(t,@"(\d+(?:\.\d+)?)\s*(?:meters?|m|متر)?\s*(?:floor(?:-to-floor)? heights?|ارتفاع طبقات|ارتفاع هر طبقه)");
            if(!dims.Success||!levels.Success)throw new ArgumentException("Offline building example: Create a 15 x 20 meter building with 5 floors and 3.2 meter floor heights.");
            var result=new ActionPlan{Summary=$"Create a {levels.Groups[1].Value}-floor building with editable slabs, columns and façades. Assumptions: world origin; floor height {(height.Success?height.Groups[1].Value:"3.2")}; regular rectangular footprint.",LengthUnit=Unit(t,context),Actions=[ModelAction.Make("create_building",new{width=double.Parse(dims.Groups[1].Value,CultureInfo.InvariantCulture),depth=double.Parse(dims.Groups[2].Value,CultureInfo.InvariantCulture),floors=int.Parse(levels.Groups[1].Value,CultureInfo.InvariantCulture),floor_height=height.Success?double.Parse(height.Groups[1].Value,CultureInfo.InvariantCulture):3.2,facade=t.Contains("solid")?"solid":"glazed",layer="AI_Building"})]};
            PlanValidator.Validate(result);return result;
        }
        if (Regex.IsMatch(t, @"\b(move|shift)\b|جابجا|جابه.?جا|منتقل"))
        {
            var ids = context.SelectedIds.Length > 0 ? context.SelectedIds : context.LastCreatedIds;
            if (ids.Length != 1) throw new ArgumentException("Select exactly one object to move, or first create one box with this panel.");
            var match = Regex.Match(t, @"[-+]?\d+(?:\.\d+)?");
            if (!match.Success) throw new ArgumentException("Specify a numeric distance, for example: Move the box 2 meters along X.");
            double distance = double.Parse(match.Value, CultureInfo.InvariantCulture);
            var axis = Regex.Match(t, @"\b[xyz]\b").Value;
            if (axis == "") throw new ArgumentException("Specify X, Y or Z explicitly.");
            if (t.Contains("negative") || t.Contains("منفی")) distance = -Math.Abs(distance);
            return new ActionPlan { Summary = $"Move object {ids[0]} by {distance} {Unit(t, context)} along {axis.ToUpperInvariant()}.", LengthUnit = Unit(t, context),
                Actions = [ModelAction.Make("move_object", new { object_id = ids[0], vector = new[] {axis == "x" ? distance : 0,axis == "y" ? distance : 0,axis == "z" ? distance : 0} })] };
        }
        if (!Regex.IsMatch(t, @"\bbox\b|باکس|جعبه|مکعب"))
            throw new ArgumentException("Offline examples support a box with three numeric dimensions, or moving one object. Connect an AI backend for free-form design requests.");
        var dimensions = Regex.Match(t, @"(\d+(?:\.\d+)?)\s*[x×*،,]\s*(\d+(?:\.\d+)?)\s*[x×*،,]\s*(\d+(?:\.\d+)?)");
        if (!dimensions.Success) throw new ArgumentException("Use three dimensions, e.g. Create a box 10 x 15 x 12 meters on layer AI_Building.");
        var layer = Regex.Match(prompt, @"\bAI_[\p{L}\p{N}_-]+\b");
        var w = double.Parse(dimensions.Groups[1].Value, CultureInfo.InvariantCulture);
        var d = double.Parse(dimensions.Groups[2].Value, CultureInfo.InvariantCulture);
        var h = double.Parse(dimensions.Groups[3].Value, CultureInfo.InvariantCulture);
        var plan = new ActionPlan { Summary = $"Create an editable {w} × {d} × {h} {Unit(t, context)} box at the origin, on { (layer.Success ? layer.Value : "AI_Building") }.", LengthUnit = Unit(t, context),
            Actions = [ModelAction.Make("create_box", new { width = w, depth = d, height = h, layer = layer.Success ? layer.Value : "AI_Building", name = "ArchiTec Box" })] };
        PlanValidator.Validate(plan);
        return plan;
    }
    private static string Unit(string t, ModelContext c)
    {
        if (Regex.IsMatch(t, @"millimeter|\bmm\b|میلی.?متر")) return "millimeters";
        if (Regex.IsMatch(t, @"centimeter|\bcm\b|سانتی.?متر")) return "centimeters";
        if (Regex.IsMatch(t, @"\bfeet\b|\bfoot\b|\bft\b|فوت")) return "feet";
        if (Regex.IsMatch(t, @"inch|اینچ")) return "inches";
        if (Regex.IsMatch(t, @"\bmeters?\b|\bmetres?\b|\bm\b|متر")) return "meters";
        return "document";
    }
    public static string Normalize(string s)
    {
        const string fa = "۰۱۲۳۴۵۶۷۸۹", ar = "٠١٢٣٤٥٦٧٨٩";
        foreach (var i in Enumerable.Range(0,10)) s = s.Replace(fa[i],(char)('0'+i)).Replace(ar[i],(char)('0'+i));
        return s.Replace('٫','.');
    }
}
