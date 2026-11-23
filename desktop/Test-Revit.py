"""Run only against the disposable Studio test project."""
import json
import subprocess
import sys
import uuid

with subprocess.Popen([sys.argv[1], "--mcp", "Revit"], stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True, encoding="utf-8") as server:
    def rpc(method, params):
        request = dict(jsonrpc="2.0", id=1, method=method, params=params)
        server.stdin.write(json.dumps(request) + "\n")
        server.stdin.flush()
        return json.loads(server.stdout.readline())["result"]
    def call(name, **args):
        data = rpc("tools/call", dict(name="revit_" + name, arguments=args))
        assert not data.get("isError"), data
        return json.loads(data["content"][0]["text"])
    try:
        rpc("initialize", {})
        rpc("tools/list", {})
        ctx = call("get_context")
        assert ctx["document"] == (sys.argv[2] if len(sys.argv)>2 else "Studio-Test-Structural"), ctx["document"]
        doc = ctx["document_id"]
        args = dict(document_id=doc, level_id=ctx["levels"][0]["id"], request_id=str(uuid.uuid4()), x1_m=0, y1_m=0, x2_m=5, y2_m=0, height_m=3)
        wall = call("create_wall", **args)
        assert abs(wall["height_m"] - 3) < 1e-6
        assert call("create_wall", **args)["wall_id"] == wall["wall_id"]
        changed = call("set_wall_height", document_id=doc, request_id=str(uuid.uuid4()), wall_id=wall["wall_id"], height_m=3.6)
        checked = call("inspect_wall", document_id=doc, wall_id=wall["wall_id"])
        assert changed["wall_id"] == wall["wall_id"] and abs(checked["height_m"] - 3.6) < 1e-6
        print("PASS live Revit: inspected project, created native wall, deduplicated retry, edited same wall, verified height.", checked)
    finally:
        server.stdin.close()
