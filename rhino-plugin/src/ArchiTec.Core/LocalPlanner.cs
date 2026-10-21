using System.Globalization;
using System.Text.RegularExpressions;

namespace ArchiTec.Core;

/// <summary>Explicit offline examples only. This is not a substitute for an AI model.</summary>
public static class LocalPlanner
{
    public static ActionPlan Plan(string prompt, ModelContext context)
    {
        var t = Normalize(prompt).ToLowerInvariant();
        if(Regex.IsMatch(t,@"\b(move|shift)\b|جابجا|جا\s*به\s*جا|منتقل"))return Move(t,context);
        if(Regex.IsMatch(t,@"\bbuilding\b|ساختمان"))
        {
            var dims=Regex.Match(t,@"(\d+(?:\.\d+)?)\s*[x×*]\s*(\d+(?:\.\d+)?)");
            var levels=Regex.Match(t,@"(\d+)\s*(?:floors?|storeys?|stories|طبقه)");
            var height=Regex.Match(t,@"(\d+(?:\.\d+)?)\s*(?:meters?|m|متر)?\s*(?:floor(?:-to-floor)? heights?|ارتفاع طبقات|ارتفاع هر طبقه)");
            if(!dims.Success||!levels.Success)throw new ArgumentException("Offline building example: Create a 15 x 20 meter building with 5 floors and 3.2 meter floor heights.");
            var result=new ActionPlan{Summary=$"Create a {levels.Groups[1].Value}-floor building with editable slabs, columns and façades. Assumptions: world origin; floor height {(height.Success?height.Groups[1].Value:"3.2")}; regular rectangular footprint.",LengthUnit=Unit(t,context),Actions=[ModelAction.Make("create_building",new{width=double.Parse(dims.Groups[1].Value,CultureInfo.InvariantCulture),depth=double.Parse(dims.Groups[2].Value,CultureInfo.InvariantCulture),floors=int.Parse(levels.Groups[1].Value,CultureInfo.InvariantCulture),floor_height=height.Success?double.Parse(height.Groups[1].Value,CultureInfo.InvariantCulture):3.2,facade=t.Contains("solid")?"solid":"glazed",layer="AI_Building"})]};
            PlanValidator.Validate(result);return result;
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
    private static ActionPlan Move(string t,ModelContext context)
    {
        var ids=context.SelectedIds.Length>0?context.SelectedIds:context.LastCreatedIds;
        if(context.Truncated||ids.Length is <1 or >100||ids.Length>1&&ids.Any(id=>!context.Objects.Any(o=>o.Id==id)))throw new ArgumentException("Select complete targets, or use the recent result in Model Assist. Incomplete target lists cannot be moved.");
        var floorMatch=Regex.Match(t,@"(?:floor|طبقه)\s*(\d+)\b|(\d+)(?:st|nd|rd|th)?\s*(?:floor|طبقه)\b");
        int? floor=floorMatch.Success?int.Parse(floorMatch.Groups[1].Success?floorMatch.Groups[1].Value:floorMatch.Groups[2].Value,CultureInfo.InvariantCulture):null;
        if(floor is null)
        {
            var names=new[]{"اول","دوم","سوم","چهارم","پنجم","ششم","هفتم","هشتم","نهم","دهم"};
            var english=new[]{"first","second","third","fourth","fifth","sixth","seventh","eighth","ninth","tenth"};
            for(var i=0;i<names.Length;i++)if(Regex.IsMatch(t,@"طبقه(?:\s*ی)?\s*"+names[i])||Regex.IsMatch(t,@"\b"+english[i]+@"[-\s]+floor\b")){floor=i+1;break;}
        }
        if(floor is null&&Regex.IsMatch(t,@"\bfloors?\b|طبقه")&&!Regex.IsMatch(t,@"all floors|همه.*طبقات|کل ساختمان"))throw new ArgumentException("Specify the floor number, or explicitly request all floors. No targets were assumed.");
        if(floor is not null){ids=ids.Where(id=>context.Objects.Any(o=>o.Id==id&&o.Floor==floor)).ToArray();if(ids.Length==0)throw new ArgumentException("That floor is not present in the current target scope.");}
        var distanceMatch=Regex.Match(t,@"([-+]?\d+(?:\.\d+)?)\s*(?:meters?\b|metres?\b|millimeters?\b|centimeters?\b|feet\b|inches\b|document units\b|[mc]?m\b|متر|میلی\s*متر|سانتی\s*متر)");
        if(!distanceMatch.Success&&floor is null)distanceMatch=Regex.Match(t,@"([-+]?\d+(?:\.\d+)?)");
        if(!distanceMatch.Success)throw new ArgumentException("Specify a numeric distance with units, for example: Move floor 5 by 2 meters along Y.");
        var distance=double.Parse(distanceMatch.Groups[1].Value,CultureInfo.InvariantCulture);var axis=Regex.Match(t,@"\b[xyz]\b").Value;var assumed=false;
        if(axis==""&&(t.Contains("back")||t.Contains("عقب"))){axis="y";assumed=true;}
        if(axis=="")throw new ArgumentException("Specify X, Y or Z. For 'back', world +Y is used and shown in the preview.");
        if(t.Contains("negative")||t.Contains("منفی"))distance=-Math.Abs(distance);
        var plan=new ActionPlan{Summary=$"Move {ids.Length} existing targets{(floor is null?"":" on floor "+floor)} by {distance} {Unit(t,context)} along {axis.ToUpperInvariant()}."+(assumed?" Assumption: back means world +Y.":""),LengthUnit=Unit(t,context),
            Actions=ids.Select(id=>ModelAction.Make("move_object",new{object_id=id,vector=new[]{axis=="x"?distance:0,axis=="y"?distance:0,axis=="z"?distance:0}})).ToList()};
        PlanValidator.Validate(plan);return plan;
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
        return s.Replace('٫','.').Replace('\u200c',' ').Replace('ي','ی').Replace('ك','ک');
    }
}
