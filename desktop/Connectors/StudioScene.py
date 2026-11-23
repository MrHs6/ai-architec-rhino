"""Run this connector from Blender's Text Editor or 3ds Max's Run Script."""
import json
import os
import time
import uuid
from pathlib import Path

try:
    import bpy
    host = "Blender"
    version = bpy.app.version_string
except ImportError:
    try:
        import FreeCAD as App
        import FreeCADGui as Gui
        import Mesh
        host = "FreeCAD"
        version = ".".join(App.Version()[:2])
    except ImportError:
        try:
            import maya.cmds as cmds
            import maya.api.OpenMaya as om
            host = "Maya"
            version = str(cmds.about(version=True)).split()[0]
        except ImportError:
            from pymxs import runtime as rt
            host = "3ds Max"
            version = str(int(rt.maxVersion()[0]) // 1000 + 1998)
    try:
        from PySide6.QtCore import QTimer
    except ImportError:
        try:
            from PySide2.QtCore import QTimer
        except ImportError:
            from PySide.QtCore import QTimer

base = Path(os.environ["LOCALAPPDATA"]) / "AIArchiTec" / "Sessions" / host / str(os.getpid())
base.mkdir(parents=True, exist_ok=True)
(base / "host.json").write_text(json.dumps(dict(pid=os.getpid(), version=version)))
receipts = {}
active_document = None
document_token = None

def execute(command, args):
    global active_document, document_token
    if host == "FreeCAD":
        scene = App.ActiveDocument
        if scene is None:
            raise ValueError("Open a FreeCAD document first.")
        if scene is not active_document:
            active_document, document_token = scene, str(uuid.uuid4())
        doc = document_token
        selected = [obj.Name for obj in Gui.Selection.getSelection()]
    elif host == "Maya":
        if document_token is None:
            document_token = str(uuid.uuid4())
        doc = document_token
        selected = cmds.ls(selection=True, long=True) or []
    elif host == "Blender":
        scene = bpy.context.scene
        doc = str(scene.as_pointer()) + ":" + str(scene.session_uid) if hasattr(scene,"session_uid") else str(scene.as_pointer()) + ":" + bpy.data.filepath
        selected = [o.name for o in bpy.context.selected_objects]
    else:
        doc = str(rt.maxFilePath) + str(rt.maxFileName) + ":" + str(rt.getHandleByAnim(rt.rootNode))
        selected = [o.name for o in rt.selection]
    if command == "get_context":
        return dict(document_id=doc, host=host, version=version, selected=selected, units="mm" if host == "FreeCAD" else "cm" if host == "Maya" else "native scene coordinates")
    if host == "3ds Max" and command in ("save_copy","export_obj","render_png"):
        if args["document_id"] != doc:
            raise ValueError("Active document changed")
        key = (doc, str(uuid.UUID(args["request_id"])), command)
        if key in receipts:
            return receipts[key]
        folder = Path(os.environ["LOCALAPPDATA"]) / "AIArchiTec" / "Exports" / key[1]
        folder.mkdir(parents=True, exist_ok=True)
        path = folder / {"save_copy":"model.max","export_obj":"model.obj","render_png":"view.png"}[command]
        if path.exists():
            raise ValueError("Output already exists. Inspect the existing file.")
        if command == "save_copy":
            rt.saveMaxFile(str(path), useNewFile=False)
        elif command == "export_obj":
            rt.exportFile(str(path), rt.Name("noPrompt"), selectedOnly=False)
        else:
            bitmap = rt.render(outputfile=str(path), vfb=False)
            rt.close(bitmap)
        if not path.is_file() or path.stat().st_size == 0:
            raise ValueError("Host produced no output file")
        receipts[key] = dict(path=str(path), bytes=path.stat().st_size)
        return receipts[key]
    if command != "create_mesh" or args["document_id"] != doc:
        raise ValueError("Inspect the active scene first.")
    token = (doc,str(uuid.UUID(args["request_id"])))
    if token in receipts:
        return receipts[token]
    verts, faces = args["vertices"], args["faces"]
    if not 3 <= len(verts) <= 20000 or not 1 <= len(faces) <= 30000:
        raise ValueError("Unsupported mesh size")
    if any(len(v)!=3 or any(not isinstance(x,(int,float)) or not abs(x)<1e9 for x in v) for v in verts):
        raise ValueError("Invalid vertex")
    if any(len(f)!=3 or len(set(f))!=3 or any(type(i)!=int or not 0<=i<len(verts) for i in f) for f in faces):
        raise ValueError("Invalid triangle")
    name = str(args["name"])[:120]
    if host == "FreeCAD":
        triangles = [[App.Vector(*verts[i]) for i in face] for face in faces]
        mesh = Mesh.Mesh(triangles)
        scene.openTransaction("Create Studio mesh")
        try:
            obj = scene.addObject("Mesh::Feature", "StudioMesh")
            obj.Label = name
            obj.Mesh = mesh
            scene.recompute()
            created = obj.Name
            scene.commitTransaction()
        except Exception:
            scene.abortTransaction()
            raise
    elif host == "Maya":
        mesh_fn = om.MFnMesh()
        old_selection = om.MGlobal.getActiveSelectionList()
        try:
            mesh_obj = mesh_fn.create([om.MPoint(*v) for v in verts], [3] * len(faces), [i for face in faces for i in face])
            # With no parent supplied MFnMesh returns the new transform.
            created = cmds.rename(om.MFnDagNode(mesh_obj).fullPathName(), name or "StudioMesh")
        finally:
            om.MGlobal.setActiveSelectionList(old_selection)
    elif host == "Blender":
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(verts, [], faces)
        mesh.update()
        obj = bpy.data.objects.new(name, mesh)
        scene.collection.objects.link(obj)
    else:
        obj = rt.mesh(vertices=[rt.Point3(*v) for v in verts], faces=[rt.Point3(*(i+1 for i in f)) for f in faces])
        obj.name = name
        rt.update(obj)
    result = dict(created=created if host in ("FreeCAD", "Maya") else str(obj.name), triangles=len(faces), vertices=len(verts))
    receipts[token] = result
    return result

def tick():
    (base / "heartbeat").touch()
    for p in list(base.glob("*.req"))[:4]:
        out = p.with_suffix(".out")
        if out.exists(): continue
        try:
            req = json.loads(p.read_text(encoding="utf-8-sig"))
            if req["expires"] < time.time(): raise ValueError("Expired")
            reply = execute(req["command"], req["args"])
        except Exception as exc:
            reply = dict(error=str(exc))
        temp = p.with_suffix(".pending")
        temp.write_text(json.dumps(reply))
        temp.replace(out)
        p.unlink(missing_ok=True)
    return 0.35

if host == "Blender":
    old = bpy.app.driver_namespace.get("architec_studio_timer")
    if old and bpy.app.timers.is_registered(old): bpy.app.timers.unregister(old)
    bpy.app.driver_namespace["architec_studio_timer"] = tick
    bpy.app.timers.register(tick, persistent=True)
else:
    import builtins
    if host == "Maya":
        def scene_changed(*_):
            global document_token
            document_token = str(uuid.uuid4())
            receipts.clear()
        for callback in getattr(builtins, "_architec_scene_callbacks", []):
            om.MMessage.removeCallback(callback)
        builtins._architec_scene_callbacks = [om.MSceneMessage.addCallback(event, scene_changed) for event in
                                              (om.MSceneMessage.kAfterOpen, om.MSceneMessage.kAfterNew)]
    old = getattr(builtins, "_architec_timer", None)
    if old: old.stop()
    timer = QTimer()
    timer.timeout.connect(tick)
    timer.start(350)
    builtins._architec_timer = timer
