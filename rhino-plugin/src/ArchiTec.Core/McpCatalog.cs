using System.Text.Json;

namespace ArchiTec.Core;

public sealed record McpImage(string Data,string MimeType,object Metadata);

public static class McpCatalog
{
    public const string Version="0.6.0-beta.10";
    public const string Instructions="Archi-Tec is a Rhino execution bridge, not another chatbot. Keep all planning, conversation and follow-ups in this MCP client. First read get_context and get_capabilities. Prefer execute_plan with a unique UUID request_id for validated native operations: execution is authorized in this client, with no extra Apply click in Rhino. Inspect actual receipts and geometry before claiming completion. If the outcome is uncertain, call get_plan_status using the SAME request_id; never resubmit a different ID blindly. propose_plan is optional preview-only; apply_plan or cancel_plan manages it from this client. Stop/cancellation stops pending work; completed batches remain undoable. Do not use another router, run arbitrary code, or substitute curves for requested solid surfaces. Use create_loft, create_mesh or create_extrusion for surfaces/solids. For follow-up edits, refresh get_context and inspect the existing GUIDs with get_objects; do not recreate the model. get_objects supports offset/next_offset pagination for large documents and reports validity/closure; set_view and get_viewport_image verify the result. For Grasshopper read get_grasshopper_context and search_grasshopper_components, then execute a grasshopper_definition action with document units in a separate batch. Return editable components and wires, not substituted Rhino geometry. Bake only when requested. No autonomous planning runs in the plugin.";
    private static object Schema(object properties,string[] required)=>new{type="object",properties,required,additionalProperties=false};
    private static readonly object Doc=new{type="integer"};
    private static readonly object Text=new{type="string"};
    private static readonly object Ids=new{type="array",items=new{type="string"},maxItems=2000};
    private static readonly object Plan=Schema(new{
        version=new{type="integer",@enum=new[]{1}},summary=Text,
        length_unit=new{type="string",@enum=new[]{"meters","millimeters","centimeters","feet","inches","document"}},
        actions=new{type="array",minItems=1,maxItems=100,items=Schema(new{tool=new{type="string",@enum=PlanValidator.Tools},parameters=new{type="object"}},["tool","parameters"])}
    },["version","summary","length_unit","actions"]);
    private static object Definition(string name,string description,object input,bool readOnly=false,bool destructive=false)=>new{name,description,inputSchema=input,annotations=new{readOnlyHint=readOnly,destructiveHint=destructive,openWorldHint=false}};
    public static readonly JsonElement Definitions=JsonSerializer.SerializeToElement(new[]{
        Definition("get_context","Read active document ID, units, layers, selected/recent targets. Does not plan or modify geometry.",Schema(new{},[]),true),
        Definition("get_capabilities","Read exact native action parameters, workflow and limits. Do not invent unsupported tools or settle for a wireframe when a solid was requested.",Schema(new{},[]),true),
        Definition("get_grasshopper_context","Activate Grasshopper and inspect open definitions, component ports, wires, output samples and solver messages. Does not change a graph. Conversation stays in this client.",Schema(new{document_id=Doc},["document_id"]),true),
        Definition("search_grasshopper_components","Discover installed supported native components and exact input/output indices before constructing a graph. Scripts, clusters and external IO are excluded.",Schema(new{document_id=Doc,query=new{type="string",maxLength=120},limit=new{type="integer",minimum=1,maximum=80}},["document_id","query"]),true),
        Definition("get_objects","Inspect explicit IDs or a layer prefix. Reports bounds, validity, closure, area and volume. Use offset (default 0) and returned next_offset to page through large models; refresh from offset 0 if the document changes between pages. Optional probe_points (document units) returns closed-Brep containment booleans; use 1–20 explicit IDs and at most 100 points. Read-only.",Schema(new{document_id=Doc,object_ids=Ids,layer_prefix=Text,include_hidden=new{type="boolean"},offset=new{type="integer",minimum=0,maximum=1000000},limit=new{type="integer",minimum=1,maximum=2000},probe_points=new{type="array",maxItems=100,items=new{type="array",minItems=3,maxItems=3,items=new{type="number"}}}},["document_id"]),true),
        Definition("post_message","Optionally display a short client status in Rhino's execution log. Continue conversation in this AI client; never ask the user to switch to an in-plugin chat.",Schema(new{document_id=Doc,message=new{type="string",minLength=1,maxLength=8000},workspace=new{type="string",@enum=new[]{"generate","analyze","assist"}}},["document_id","message"])),
        Definition("execute_plan","Execute a validated native action batch in Rhino, using this client's normal tool authorization. No local Apply click. Supply a new UUID request_id once; retries with that same ID return its recorded receipt without duplicating geometry. Read status after timeout. No scripts.",Schema(new{document_id=Doc,request_id=Text,plan=Plan},["document_id","request_id","plan"]),destructive:true),
        Definition("propose_plan","Optional native viewport preview only. Continue in this client: apply_plan executes the preview, cancel_plan discards it. No need to switch to Rhino or ask for another Continue.",Schema(new{document_id=Doc,plan=Plan},["document_id","plan"])),
        Definition("apply_plan","Commit an optional preview from this client after normal tool authorization. Uses the exact prepared document/units/target fingerprints and records one undoable result. Repeating the same request_id returns the existing receipt.",Schema(new{document_id=Doc,request_id=Text},["document_id","request_id"]),destructive:true),
        Definition("cancel_plan","Discard a preview from this client. Does not undo an already applied batch.",Schema(new{request_id=Text},["request_id"])),
        Definition("get_plan_status","Read processing, awaiting_approval, applied, cancelled or failed receipt by request_id. Check before any retry after an uncertain outcome.",Schema(new{request_id=Text},["request_id"]),true),
        Definition("set_view","Frame explicit visible object IDs, or the latest result, in Perspective. Optional camera_location and camera_target are in document units. Changes only the viewport.",Schema(new{document_id=Doc,object_ids=Ids,camera_location=new{type="array",items=new{type="number"},minItems=3,maxItems=3},camera_target=new{type="array",items=new{type="number"},minItems=3,maxItems=3},display_mode=new{type="string",@enum=new[]{"Shaded","Rendered","Arctic","Wireframe"}}},["document_id"])),
        Definition("get_viewport_image","Capture the active viewport as a PNG for visual verification. Does not create geometry or change the view.",Schema(new{document_id=Doc},["document_id"]),true)
    });

    public static object Capabilities=>new{
        version=Version,workflow="Client plans and executes. Rhino validates, runs native actions, records Undo and returns receipts. No model API is called by MCP.",
        limits=new{actions_per_batch=100,http_bytes=2_000_000,loft_profiles=64,mesh_vertices=20000,mesh_faces=40000},
        actions=new Dictionary<string,string>{
            [GrasshopperPlan.Tool]="Use a SEPARATE plan with exactly this one action and length_unit=document. Parameters: name, optional definition_id (runtime integer from a receipt, only bridge-created graphs may be replaced), nodes:[{id:local_alias,kind:component,component_id:discovered_GUID,x,y,nickname?,inputs:[{index,type:number|integer|boolean|text|point|vector|plane_xy|interval,path:[0],values:[...]}]}], wires:[{from:alias,output:0,to:alias,input:0}], bake_nodes:[aliases] (optional; only when explicitly requested). Slider node: {id,kind:slider,x,y,nickname,value,minimum,maximum}, no component_id. Use exact ports from search_grasshopper_components, acyclic graph, at most 200 nodes/500 wires. Read get_grasshopper_context first. Results are editable GH components/wires; no Rhino objects unless bake_nodes explicitly supplied. Graph Undo is in Grasshopper; bake Undo in Rhino. Preview validates a detached graph; solving occurs on authorized apply. Errors abort without committing. No scripts or third-party components.",
            ["create_loft"]="profiles: ordered arrays of [x,y,z] points; closed_profiles=true by default; curve_degree=1 (polyline) or 3 (interpolated); cap=true requires closed planar ends. Returns native Brep, no construction curves.",
            ["create_mesh"]="vertices: [x,y,z][]; faces: 0-based index triangles/quads; require_solid=true by default rejects open/nonmanifold geometry. Returns one native Mesh.",
            ["create_extrusion"]="points: planar polygon coordinates; vector:[x,y,z] extrusion direction; cap=true by default. Inline profile produces one native Brep, no extra curve.",
            ["create_boolean"]="operation: union|difference|intersection|join; object_ids: Brep source IDs; cutter_ids for difference/intersection. Creates a new Brep, preserves sources. Hide sources explicitly after verification.",
            ["create_sweep"]="rail_id: existing Curve ID; section_ids: ordered Curve IDs; cap:boolean. Native one-rail sweep.",
            ["create_revolve"]="points: profile coordinates; axis_start,axis_end:[x,y,z]; angle_degrees:(0,360]; cap:boolean.",
            ["create_pipe"]="points: rail coordinates; radius; smooth:boolean. Returns capped native Brep tube.",
            ["create_offset"]="object_id: Brep source; distance: signed; solid:boolean. Native Brep offset.",
            ["create_geometry"]="geometry_json: native Rhino CommonObject.ToJSON / compatible rhino3dm encoding as string. Validated Brep, Surface, Curve, Extrusion, Mesh or SubD only. No scripts/files/commands; coordinates scaled from length_unit.",
            ["rotate_object"]="object_id,axis_origin,axis_direction,angle_degrees. Preserves ID.",
            ["copy_object"]="object_id,vector:[x,y,z]; optional layer,name. Creates translated copy.",
            ["create_box"]="width,depth,height; optional origin:[x,y,z],layer,name.",
            ["create_building"]="width,depth,floors,floor_height; optional slab_thickness,facade:glazed|solid|none,origin,layer. Limited rectangular building template.",
            ["create_line"]="start,end:[x,y,z]; optional layer,name. A line is not a solid.",
            ["create_polyline"]="points:[x,y,z][]; optional closed,layer,name. This creates only a construction curve.",
            ["create_curve"]="points:[x,y,z][]; optional closed,layer,name. This creates only a construction curve.",
            ["extrude_curve"]="object_id of existing planar curve,height,optional cap,layer,name. Extrudes along +Z.",
            ["move_object"]="object_id,vector:[x,y,z]. Preserves object ID.",
            ["scale_object"]="object_id,factor,center:[x,y,z]. Preserves object ID.",
            ["set_visibility"]="object_id,visible:boolean. Reversibly hide/show an editable target; does not delete it.",
            ["create_layer"]="name; optional color:[R,G,B] integer 0..255 and transparency:0..1.",
            ["select_objects"]="object_ids:string[].",
            ["delete_objects"]="object_ids:string[] (1–100 explicitly authorized editable targets). Native document deletion, included in batch Undo. Never deletes files; inspect IDs and preserve a document backup before replacing a model.",
            ["get_object_properties"]="object_id. Prefer get_objects for read-only inspection without an action batch."
        }
    };
}
