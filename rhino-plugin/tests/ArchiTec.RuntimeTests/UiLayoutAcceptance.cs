using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ArchiTec.Core;
using ArchiTec.RhinoPlugin.Views;
using Rhino;

namespace ArchiTec.RuntimeTests;

/// <summary>Detached, real WPF layout checks. Does not read profiles, call a provider or alter Rhino geometry.</summary>
public static class UiLayoutAcceptance
{
    public static void Schedule(string outputDirectory)
    {
        var output=Path.GetFullPath(outputDirectory);
        EventHandler? idle=null;
        idle=(_,_)=>
        {
            RhinoApp.Idle-=idle;
            var checks=new List<object>();var status="running";string? error=null;
            Directory.CreateDirectory(output);
            void Save()=>File.WriteAllText(Path.Combine(output,"ui-layout-acceptance.json"),JsonSerializer.Serialize(new{status,plugin_version=McpCatalog.Version,cloud_requests=0,geometry_mutations=0,checks,error},Wire.Json));
            void Check(bool ok,string name,object? data=null)
            {
                if(!ok)throw new InvalidOperationException(name);
                checks.Add(new{name,data});Save();RhinoApp.WriteLine("PASS "+name);
            }
            try
            {
                Save();
                foreach(var size in new[]{new Size(560,780),new Size(420,640),new Size(350,540)})
                {
                    var view=new StudioView();
                    const string modelId="fixture/readable-model",modelName="Readable provider model";
                    view.SetCatalog([new(modelId,modelName,false)],modelId,"OpenRouter");
                    view.SetRecentResult("Recent native result · 1 object");
                    for(var i=0;i<12;i++)view.AddMessage(i%2==0?"user":"assistant",$"Layout message {i+1}. "+string.Join(" ",Enumerable.Repeat("A readable conversation has room to explain the result and the next edit.",4)));
                    Layout(view,size);
                    var scroll=Element<ScrollViewer>(view,"ConversationScroll");
                    var composer=Element<Border>(view,"ComposerCard");
                    var header=Element<Grid>(view,"BrandHeader");
                    var options=Element<Border>(view,"WorkspaceOptionsCard");
                    var conversation=Element<StackPanel>(view,"Conversation");
                    var preview=Element<Border>(view,"PreviewCard");
                    Check(AncestorScroll(options)==scroll&&AncestorScroll(conversation)==scroll&&AncestorScroll(preview)==scroll&&AncestorScroll(Element<StackPanel>(view,"ConnectionControls"))==scroll,$"Menus, options, messages and preview share one scroll at {size.Width} x {size.Height}");
                    Check(AncestorScroll(composer) is null&&AncestorScroll(header) is null,$"Brand and composer remain outside the scrolling content at {size.Width} x {size.Height}");
                    Check(scroll.ViewportHeight>=180&&conversation.ActualHeight>scroll.ViewportHeight,$"Conversation has a useful shared viewport and no tiny independent box at {size.Width} x {size.Height}",new{viewport_height=scroll.ViewportHeight,conversation_height=conversation.ActualHeight});
                    var headerY=header.TranslatePoint(new Point(),view).Y;
                    var composerY=composer.TranslatePoint(new Point(),view).Y;
                    scroll.ScrollToTop();Layout(view,size);
                    var before=Element<StackPanel>(view,"ConnectionControls").TranslatePoint(new Point(),scroll).Y;
                    scroll.ScrollToEnd();Layout(view,size);
                    var after=Element<StackPanel>(view,"ConnectionControls").TranslatePoint(new Point(),scroll).Y;
                    Check(scroll.VerticalOffset>0&&after<before-100&&Math.Abs(header.TranslatePoint(new Point(),view).Y-headerY)<1&&Math.Abs(composer.TranslatePoint(new Point(),view).Y-composerY)<1,$"Scrolling moves upper menus away while brand and composer stay fixed at {size.Width} x {size.Height}");
                    var selector=Element<ComboBox>(view,"ModelSelector");
                    var selectionText=Descendants<TextBlock>(selector).Select(t=>t.Text).ToArray();
                    Check(selectionText.Contains(modelName)&&selectionText.All(t=>!t.Contains("ModelDescriptor",StringComparison.Ordinal)),$"Selected model displays its readable name, not its record serialization at {size.Width} x {size.Height}");
                    var prompt=Element<TextBox>(view,"PromptInput");
                    prompt.Text=string.Join("\n",Enumerable.Repeat("A longer architectural request with dimensions and scope.",24));Layout(view,size);
                    Check(prompt.AcceptsReturn&&prompt.MinHeight>=80&&prompt.MaxHeight<=180&&prompt.ActualHeight<=181&&prompt.VerticalScrollBarVisibility==ScrollBarVisibility.Auto&&composer.TranslatePoint(new Point(),view).Y+composer.ActualHeight<=size.Height+1,$"Long multiline composer remains bounded and reachable at {size.Width} x {size.Height}",new{prompt_height=prompt.ActualHeight});
                }

                foreach(var size in new[]{new Size(560,780),new Size(420,640),new Size(350,540)})
                {
                var routes=new StudioView();routes.SetCatalog([new("fixture/route-model","Route fixture model",false)],"fixture/route-model","OpenRouter");
                routes.SetRecentResult("Recent result");
                foreach(var workspace in new[]{"generate","analyze","assist"})
                {
                    routes.SetWorkspace(workspace,false);Layout(routes,size);
                    Check(Element<ScrollViewer>(routes,"ConversationScroll").ViewportHeight>180&&Element<Border>(routes,"ComposerCard").Visibility==Visibility.Visible,$"{workspace} preserves the fixed composer and shared workspace");
                }
                Element<ComboBox>(routes,"ChatRouteSelector").SelectedIndex=1;routes.SetRecentResult("Refreshed recent result");Layout(routes,size);
                Check(Element<Border>(routes,"ComposerCard").Visibility==Visibility.Collapsed&&Element<Border>(routes,"WorkspaceOptionsCard").Visibility==Visibility.Collapsed&&Element<Border>(routes,"RecentResultBar").Visibility==Visibility.Collapsed&&AncestorScroll(Element<Border>(routes,"McpWorkspaceCard"))==Element<ScrollViewer>(routes,"ConversationScroll"),"MCP shows a scrolling execution workspace without local chat or recent-result edit controls");
                routes.SelectApiRoute();Element<ComboBox>(routes,"TargetSelector").SelectedIndex=1;routes.SetRecentResult("Recent result");Layout(routes,size);
                Check(Element<Border>(routes,"RecentResultBar").Visibility==Visibility.Collapsed&&Element<Button>(routes,"AnalyzeNav").Visibility==Visibility.Collapsed&&Element<StackPanel>(routes,"AssistOptions").Visibility==Visibility.Collapsed&&routes.PlanningOptions.Contains("Grasshopper",StringComparison.Ordinal),"Grasshopper keeps its own guidance and hides native recent-result controls");
                routes.SetApiConnectionError("OpenRouter","Fixture network error.");Element<ComboBox>(routes,"ChatRouteSelector").SelectedIndex=1;routes.SelectApiRoute();Layout(routes,size);
                Check(Element<ComboBox>(routes,"ModelSelector").Visibility==Visibility.Collapsed,"Route changes keep stale model selectors hidden after an API failure");
                routes.ShowApi();routes.SetCatalog([new("fixture/profile-model","Readable profile model",false)],"fixture/profile-model","OpenRouter");Layout(routes,size);
                Check(Descendants<TextBlock>(Element<ComboBox>(routes,"ProfileModelSelector")).Any(t=>t.Text=="Readable profile model"),"API profile selected model uses the readable-name template too");
                }

                status="passed";Save();
            }
            catch(Exception failure){status="failed";error=failure is InvalidOperationException?failure.Message:"Detached layout acceptance could not complete.";Save();RhinoApp.WriteLine("UI layout acceptance stopped; see the report.");}
        };
        RhinoApp.Idle+=idle;
    }
    private static void Layout(FrameworkElement view,Size size){view.Measure(size);view.Arrange(new Rect(new Point(),size));view.UpdateLayout();}
    private static T Element<T>(StudioView view,string name)where T:FrameworkElement=>(T)(view.FindName(name)??throw new InvalidOperationException("Required view element is unavailable: "+name));
    private static ScrollViewer? AncestorScroll(DependencyObject item)
    {
        for(var parent=VisualTreeHelper.GetParent(item);parent is not null;parent=VisualTreeHelper.GetParent(parent))if(parent is ScrollViewer scroll)return scroll;
        return null;
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);if(child is T target)yield return target;
            foreach(var nested in Descendants<T>(child))yield return nested;
        }
    }
}
