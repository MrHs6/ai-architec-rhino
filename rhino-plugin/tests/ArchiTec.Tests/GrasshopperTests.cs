using System.Net;
using System.Text;
using System.Text.Json;
using ArchiTec.Core;

static class GrasshopperTests
{
    public static async Task<int> Run()
    {
        int count=0;
        void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
        void Reject(Action action,string name){try{action();}catch(Exception e) when(e is ArgumentException or JsonException or InvalidOperationException){Check(true,name);return;}throw new Exception("Accepted invalid graph: "+name);}
        var component=Guid.NewGuid();
        var graph=new GrasshopperDefinition{Name="Parametric box",Nodes=[new(){Id="width",Kind="slider",Value=2,Maximum=20},new(){Id="box",ComponentId=component,X=300}],Wires=[new("width",0,"box",0)]};
        ActionPlan Plan(GrasshopperDefinition definition)=>new(){Summary="Create parametric definition",LengthUnit="document",Actions=[ModelAction.Make(GrasshopperPlan.Tool,definition)]};
        var valid=Plan(graph);PlanValidator.Validate(valid);
        Check(GrasshopperPlan.IsGrasshopper(valid),"Grasshopper graph validates as an isolated document-unit plan");
        PlanValidator.Validate(Plan(graph with{DefinitionId=0}));
        Check(true,"Zero is a valid first Grasshopper RuntimeID for follow-up edits");
        Check(GrasshopperPlan.Read(valid.Actions[0].Parameters).Wires.Single().To=="box","Graph aliases and wires survive wire-format roundtrip");
        Reject(()=>PlanValidator.Validate(valid with{LengthUnit="meters"}),"Grasshopper rejects implicit unit conversion");
        Reject(()=>PlanValidator.Validate(valid with{Actions=[..valid.Actions,ModelAction.Make("create_box",new{width=1,depth=2,height=3})]}),"Grasshopper and Rhino mutations cannot share a transaction");
        Reject(()=>PlanValidator.Validate(Plan(graph with{Nodes=[graph.Nodes[0],graph.Nodes[0]]})),"Duplicate graph aliases rejected");
        Reject(()=>PlanValidator.Validate(Plan(graph with{Wires=[new("missing",0,"box",0)]})),"Missing wire endpoints rejected");
        Reject(()=>PlanValidator.Validate(Plan(graph with{Wires=[..graph.Wires,new("box",0,"width",0)]})),"Feedback cycles rejected before solver entry");
        Reject(()=>PlanValidator.Validate(Plan(graph with{BakeNodes=["unknown"]})),"Bake cannot select an implicit or unknown node");
        Reject(()=>PlanValidator.Validate(Plan(graph with{Nodes=[graph.Nodes[0] with{Value=100},graph.Nodes[1]]})),"Slider values outside range rejected");
        Reject(()=>PlanValidator.Validate(Plan(graph with{Nodes=[graph.Nodes[0],graph.Nodes[1] with{ComponentId=null}]})),"Components require a discoverable GUID");
        Reject(()=>PlanValidator.Validate(Plan(graph with{Nodes=[graph.Nodes[0],graph.Nodes[1] with{Inputs=[new(){Index=0,Type="number",Values=JsonSerializer.SerializeToElement(new[]{2})}]}]})),"A wired input cannot silently ignore persistent values");
        Reject(()=>GrasshopperPlan.Read(JsonDocument.Parse("{\"name\":\"a\",\"name\":\"b\",\"nodes\":[]}").RootElement),"Duplicate nested JSON graph fields rejected");
        Reject(()=>GrasshopperPlan.Read(JsonDocument.Parse("{\"name\":\"a\",\"nodes\":[],\"code\":\"anything\"}").RootElement),"Graph contract exposes no executable-code field");
        var context=new ModelContext(1,"Meters","Default",[],0,[],[],[],false);
        Reject(()=>WorkspacePolicy.Validate("generate",valid,context),"API Grasshopper plan requires the selected Grasshopper target");
        WorkspacePolicy.Validate("generate",valid,context with{Grasshopper=new{target="grasshopper"}});
        Check(true,"API Grasshopper target accepts validated graph creation and edits");
        Reject(()=>WorkspacePolicy.Validate("analyze",valid,context with{Grasshopper=new{}}),"Grasshopper analysis stays read-only");
        var definitions=McpCatalog.Definitions.EnumerateArray().Select(d=>d.GetProperty("name").GetString()).ToArray();
        Check(definitions.Contains("get_grasshopper_context")&&definitions.Contains("search_grasshopper_components"),"MCP exposes graph inspection and installed component discovery");
        var schema=PlannerInstructions.Schema.GetRawText();Check(schema.Contains(GrasshopperPlan.Tool),"Every API protocol shares the Grasshopper action schema");
        using var handler=new GraphProviderHandler(valid);
        using var provider=new ProviderClient("https://example.com/v1/","test-only-key",false,handler);
        await provider.Models(default);
        var reply=await provider.Plan(new PlanRequest("Build a parametric box","test-model","generate",context with{Grasshopper=new{definition_id=12,components=new[]{component}}},[]),default);
        Check(reply.Plan is not null&&GrasshopperPlan.IsGrasshopper(reply.Plan),"Mock API transport parses a real structured Grasshopper plan");
        Check(handler.Body.Contains("grasshopper")&&handler.Body.Contains(component.ToString()),"API request includes graph context and real component IDs");
        return count;
    }
    private sealed class GraphProviderHandler(ActionPlan plan):HttpMessageHandler
    {
        public string Body="";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            if(request.Method==HttpMethod.Get)return Json("{\"data\":[{\"id\":\"test-model\"}]}");
            Body=await request.Content!.ReadAsStringAsync(token);
            var reply=new{message="Review this definition plan",plan=new{version=1,summary=plan.Summary,length_unit="document",actions=plan.Actions.Select(a=>new{tool=a.Tool,parameters_json=a.Parameters.GetRawText()})}};
            return Json(JsonSerializer.Serialize(new{choices=new[]{new{finish_reason="stop",message=new{content=JsonSerializer.Serialize(reply)}}}}));
        }
        private static HttpResponseMessage Json(string content)=>new(HttpStatusCode.OK){Content=new StringContent(content,Encoding.UTF8,"application/json")};
    }
}
