"""Read-only MCP protocol smoke test. Usage: python tests/mcp_engineering_skills_smoke.py path/to/DotNetCad.Mcp.dll"""
import json
import queue
import subprocess
import sys
import threading
from pathlib import Path

server = subprocess.Popen(["dotnet", str(Path(sys.argv[1]).resolve())], stdin=subprocess.PIPE,
                          stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, encoding="utf-8")
responses = queue.Queue()

def read_output():
    for line in server.stdout:
        responses.put(json.loads(line))

threading.Thread(target=read_output, daemon=True).start()
request_id = 0

def send(method, params, notification=False):
    global request_id
    request_id += 1
    request = {"jsonrpc": "2.0", "method": method, "params": params}
    if not notification:
        request["id"] = request_id
    server.stdin.write(json.dumps(request) + "\n")
    server.stdin.flush()
    if notification:
        return None
    while True:
        reply = responses.get(timeout=20)
        if reply.get("id") == request_id:
            assert "error" not in reply, reply
            return reply["result"]

def call(tool_name, **arguments):
    return send("tools/call", {"name": tool_name, "arguments": arguments})

try:
    init = send("initialize", {"protocolVersion": "2024-11-05", "capabilities": {},
                              "clientInfo": {"name": "engineering-skills-smoke", "version": "1"}})
    assert "list_engineering_skills" in init["instructions"]
    send("notifications/initialized", {}, notification=True)
    names = {tool["name"] for tool in send("tools/list", {})["tools"]}
    assert {"list_engineering_skills", "read_engineering_skill"} <= names
    listing = call("list_engineering_skills")
    catalog = json.loads(listing["content"][0]["text"])
    assert len(catalog["skills"]) == 9
    for skill in catalog["skills"]:
        result = call("read_engineering_skill", name=skill["name"])
        assert not result.get("isError"), result
        assert "name: " + skill["name"] in result["content"][0]["text"]
    for invalid in ("../Program.cs", "..\\Program.cs", "C:/Windows/win.ini", "missing-skill", ""):
        assert call("read_engineering_skill", name=invalid).get("isError"), invalid
    print("PASS: initialize guidance, discovery, 9 published skills readable, invalid paths/names rejected; no CAD mutations.")
finally:
    server.terminate()
    try:
        server.wait(timeout=5)
    except subprocess.TimeoutExpired:
        server.kill()
        server.wait()
