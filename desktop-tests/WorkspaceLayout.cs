using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Studio;
internal static class WorkspaceLayout
{
    public static void Verify(string output)
    {
        Exception? error=null;
        var thread=new Thread(()=>{
            try{
                var application=new Application();
                var window=new StudioWindow("fixture");
                var hosts=(ListBox)window.FindName("Host");
                if(hosts.Items.Count<12)throw new Exception("Missing host rows.");
                var edit=(TextBox)window.FindName("Prompt");
                hosts.SelectedIndex=0;edit.Text="Review the envelope.";
                hosts.SelectedIndex=1;hosts.SelectedIndex=0;
                if(edit.Text!="Review the envelope.")throw new Exception("Lost draft");
                var edition=(ComboBox)window.FindName("Edition");edition.SelectedIndex=2;
                if(edit.Text.Length!=0)throw new Exception("Version draft isolation failed");
                edition.SelectedIndex=0;
                window.SetRail(true);
                if(((FrameworkElement)window.FindName("Rail")).Visibility!=Visibility.Visible)throw new Exception("Rail failed to open.");
                window.SetRail(false);
                var blocks=new StackPanel();ChatOutput.Render(blocks,"# Design review\n| A | B |\n| --- | --- |\n| 1 | 2 |\n```json\n{}\n```",(_,_)=>{});
                if(blocks.Children.OfType<Button>().Count()!=2 || !blocks.Children.OfType<ScrollViewer>().Any())throw new Exception("Missing table or file export.");
                if(ChatOutput.Csv(new[]{new[]{"a,b","c\"d"}})!="\"a,b\",\"c\"\"d\"")throw new Exception("CSV quoting");
                var content=(FrameworkElement)window.Content;
                var settings=(Expander)window.FindName("Settings");
                settings.IsExpanded=true;
                content.Measure(new Size(410,570));content.Arrange(new Rect(0,0,410,570));content.UpdateLayout();
                foreach(string name in new[]{"ChatModel","BuildModel"}){
                    var combo=(ComboBox)window.FindName(name);
                    combo.ApplyTemplate();
                    if(combo.Template.FindName("PART_EditableTextBox",combo) is not TextBox editor || editor.Visibility!=Visibility.Visible)throw new Exception("Missing manual model input");
                    editor.Text="vendor/"+name; if(combo.Text!=editor.Text)throw new Exception("Manual model ID was lost");
                }
                if(!settings.IsVisible && settings.Visibility!=Visibility.Visible)throw new Exception("Settings not accessible");
                if(edit.ActualWidth<150 || edit.ActualHeight<32)throw new Exception("Compact composer unusable");
                settings.IsExpanded=false;
                window.SetRail(true);
                content.Measure(new Size(990,720));content.Arrange(new Rect(0,0,990,720));content.UpdateLayout();
                var bmp=new RenderTargetBitmap(990,720,96,96,PixelFormats.Pbgra32);bmp.Render(content);
                var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));
                using var stream=File.Create(output);png.Save(stream);
                window.Close();
                Console.WriteLine("PASS desktop layout, host catalog, version and draft isolation.");
            }catch(Exception ex){error=ex;}
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw error;
    }
}
