using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using Microsoft.Win32;

namespace Studio;

internal static class ChatOutput
{
    internal static TextBox Text(string value,bool code=false)=>new(){
        Text=value,IsReadOnly=true,TextWrapping=code?TextWrapping.NoWrap:TextWrapping.Wrap,
        HorizontalScrollBarVisibility=code?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled,
        FontFamily=new FontFamily(code?"Consolas":"Segoe UI"),FontSize=14,
        Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0,4,0,4),
        FlowDirection=!code && Regex.IsMatch(value,@"^[^a-zA-Z\p{IsArabic}]*[\p{IsArabic}]")?FlowDirection.RightToLeft:FlowDirection.LeftToRight};
    static RichTextBox Paragraphs(string text)
    {
        var document=new FlowDocument {PagePadding=new Thickness(0),FontSize=14,LineHeight=21};
        foreach(string line in text.Split('\n')){
            var paragraph=new Paragraph {Margin=new Thickness(0,0,0,2),FlowDirection=Regex.IsMatch(line,@"^[^a-zA-Z\p{IsArabic}]*[\p{IsArabic}]")?FlowDirection.RightToLeft:FlowDirection.LeftToRight};
            string display=line.StartsWith("- " )?"•  "+line[2..]:line;
            foreach(var span in Regex.Split(display,@"(\*\*[^*]+\*\*|`[^`]+`)")){
                if(span.StartsWith("**") && span.EndsWith("**") && span.Length>4)paragraph.Inlines.Add(new Bold(new Run(span[2..^2])));
                else if(span.StartsWith("`") && span.EndsWith("`") && span.Length>2)paragraph.Inlines.Add(new Run(span[1..^1]){FontFamily=new FontFamily("Consolas"),Foreground=Brushes.Plum});
                else paragraph.Inlines.Add(new Run(span));
            }
            document.Blocks.Add(paragraph);
        }
        return new RichTextBox(document){IsReadOnly=true,Foreground=Brushes.WhiteSmoke,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0)};
    }
    internal static string Csv(IEnumerable<string[]> rows)=>string.Join("\r\n",rows.Select(row=>string.Join(",",row.Select(c=>"\""+c.Replace("\"","\"\"")+"\""))));
    internal static string[] Cells(string line)=>Regex.Split(line.Trim().Trim('|'),@"(?<!\\)\|").Select(c=>c.Trim().Replace("\\|","|")).ToArray();
    internal static void Render(StackPanel view,string message,Action<string,string> save)
    {
        var lines=message.Replace("\r\n","\n").Split('\n');
        var paragraph=new List<string>();
        void Flush(){if(paragraph.Count>0){view.Children.Add(Paragraphs(string.Join("\n",paragraph)));paragraph.Clear();}}
        void Download(string title,string filename,string content){
            var b=new Button{Content=title,HorizontalAlignment=HorizontalAlignment.Left};
            b.Click+=(_,_)=>save(filename,content);view.Children.Add(b);
        }
        for(int i=0;i<lines.Length;i++){
            var line=lines[i];
            if(line.TrimStart().StartsWith("```")){
                Flush();var language=line.Trim()[3..].Trim();var block=new List<string>();
                while(++i<lines.Length&&!lines[i].TrimStart().StartsWith("```"))block.Add(lines[i]);
                string content=string.Join("\n",block);
                view.Children.Add(Text(content,true));
                var ext=language switch{"json" or "csv" or "md" or "svg" or "xml" or "html" or "css" or "py" or "cs" or "js" or "yaml"=>language,_=>"txt"};
                Download("Save "+ext.ToUpperInvariant()+"…","output."+ext,content);
            }else if(line.Contains('|') && i+1<lines.Length && lines[i+1].Contains('|') && Cells(lines[i+1]).All(c=>Regex.IsMatch(c,@"^:?-{3,}:?$"))){
                Flush();var rows=new List<string[]>{Cells(line)};i++;
                while(i+1<lines.Length && lines[i+1].Contains('|'))rows.Add(Cells(lines[++i]));
                var grid=new Grid();int n=rows.Max(r=>r.Length);
                for(int c=0;c<n;c++)grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto,MaxWidth=310});
                for(int r=0;r<rows.Count;r++){
                    grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
                    for(int c=0;c<rows[r].Length;c++){
                        var cell=new Border{Padding=new Thickness(9,5,9,5),Background=r==0?new SolidColorBrush(Color.FromRgb(47,43,57)):Brushes.Transparent,BorderBrush=new SolidColorBrush(Color.FromRgb(58,57,65)),BorderThickness=new Thickness(0,0,0,.5),Child=new TextBlock{Text=rows[r][c],TextWrapping=TextWrapping.Wrap,FontWeight=r==0?FontWeights.SemiBold:FontWeights.Normal}};
                        Grid.SetRow(cell,r);Grid.SetColumn(cell,c);grid.Children.Add(cell);
                    }
                }
                view.Children.Add(new ScrollViewer{Content=grid,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled});
                Download("Export table · CSV…","table.csv",Csv(rows));
            }else if(Regex.IsMatch(line,@"^#{1,6} ")){
                Flush();var heading=Text(line.TrimStart('#',' '));heading.FontWeight=FontWeights.SemiBold;heading.FontSize=17;view.Children.Add(heading);
            }else paragraph.Add(line);
        }
        Flush();
    }
}
