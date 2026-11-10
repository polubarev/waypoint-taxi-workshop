#!/usr/bin/env python3
"""Expose read-only GitHub PR tools to Bob over MCP stdio."""

import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path
from urllib.parse import quote, urlencode


HOST = "github.com"
REPOSITORY = "polubarev/waypoint-taxi-workshop"
MAX_RESPONSE_BYTES = 512 * 1024
SHA = re.compile(r"[0-9a-f]{40}")
PROTOCOL_VERSIONS = {"2024-11-05", "2025-03-26", "2025-06-18", "2025-11-25"}


class ToolError(Exception):
    pass


def github_get(path, *, accept="application/vnd.github+json"):
    gh = shutil.which("gh")
    if gh is None:
        gh = next((str(p) for p in map(Path, ("/opt/homebrew/bin/gh", "/usr/local/bin/gh"))
                   if p.is_file() and os.access(p, os.X_OK)), None)
    if gh is None:
        raise ToolError("GitHub CLI is required in Bob's environment.")
    command = [gh, "api", "--hostname", HOST, "--method", "GET",
               "--header", f"Accept: {accept}", f"repos/{REPOSITORY}/{path}"]
    try:
        result = subprocess.run(command, stdin=subprocess.DEVNULL, capture_output=True,
                                timeout=25, env={**os.environ, "GH_PROMPT_DISABLED": "1"})
    except subprocess.TimeoutExpired as exc:
        raise ToolError("The GitHub read timed out.") from exc
    except OSError as exc:
        raise ToolError("GitHub CLI could not start in Bob's environment.") from exc
    if result.returncode:
        match = re.search(rb"HTTP (\d{3})", result.stderr)
        status = f" (HTTP {match[1].decode()})" if match else ""
        raise ToolError(f"GitHub read failed{status}. Check the existing gh login for {HOST} and repository access.")
    if len(result.stdout) > MAX_RESPONSE_BYTES:
        raise ToolError("GitHub response exceeded the review tool's size limit.")
    return result.stdout


def github_json(path):
    return json.loads(github_get(path))


def pr_number(arguments):
    number = arguments.get("number")
    if type(number) is not int or number < 1:
        raise ToolError("Provide a positive pull request number.")
    return number


def summary(pr):
    return {
        "number": pr["number"],
        "title": pr["title"],
        "url": pr["html_url"],
        "state": pr["state"],
        "draft": pr.get("draft", False),
        "base_branch": pr["base"]["ref"],
        "base_sha": pr["base"]["sha"],
        "head_branch": pr["head"]["ref"],
        "head_sha": pr["head"]["sha"],
    }


def list_pull_requests(arguments):
    state = arguments.get("state", "open")
    if state not in ("open", "closed", "all"):
        raise ToolError("State must be open, closed or all.")
    query = urlencode({"state": state, "per_page": 20})
    return json.dumps([summary(pr) for pr in github_json(f"pulls?{query}")], indent=2)


def read_pull_request(arguments):
    path = f"pulls/{pr_number(arguments)}"
    pr = github_json(path)
    metadata = summary(pr)
    diff = github_get(path, accept="application/vnd.github.v3.diff").decode("utf-8")
    if diff.strip() and not diff.startswith("diff --git "):
        raise ToolError("GitHub did not return a unified PR diff.")
    after = summary(github_json(path))
    if any(metadata[field] != after[field] for field in ("base_sha", "head_sha")):
        raise ToolError("The PR changed while GitHub was returning its diff. Read it again.")
    metadata["description"] = pr.get("body") or ""
    metadata["changed_files"] = pr["changed_files"]
    return json.dumps(metadata, indent=2) + "\n\n" + diff


def read_pull_request_file(arguments):
    number = pr_number(arguments)
    head = arguments.get("head_sha")
    path = arguments.get("path")
    if not isinstance(head, str) or not SHA.fullmatch(head):
        raise ToolError("Use the head_sha returned by read_pull_request.")
    if (not isinstance(path, str) or not path or len(path) > 1024
            or "\\" in path or any(ord(c) < 32 for c in path)
            or any(part in ("", ".", "..") for part in path.split("/"))):
        raise ToolError("Provide a repository-relative file path.")
    filename = path.rsplit("/", 1)[-1]
    if (filename.endswith((".key", ".pem"))
            or (filename != ".env.example" and (filename == ".env" or filename.startswith(".env.")))):
        raise ToolError("That file is outside this review tool's scope.")
    pr = github_json(f"pulls/{number}")
    if pr["head"]["sha"] != head:
        raise ToolError("The PR head changed. Read the PR again before reviewing source.")
    if (pr["head"].get("repo") or {}).get("full_name", "").lower() != REPOSITORY:
        raise ToolError("The PR head is in a different repository. Review its diff with the PR tool.")
    source = github_get(f"contents/{quote(path, safe='/')}?{urlencode({'ref': head})}",
                        accept="application/vnd.github.v3.raw")
    return f"{path} at {head}\n\n" + source.decode("utf-8")


NUMBER = {"type": "integer", "minimum": 1, "description": "Pull request number in the workshop repository."}
TOOLS = {
    "list_pull_requests": (
        list_pull_requests,
        "List the 20 most recent pull requests in the connected GitHub workshop repository.",
        {"state": {"type": "string", "enum": ["open", "closed", "all"], "default": "open"}},
        [],
    ),
    "read_pull_request": (
        read_pull_request,
        "Read a pull request's description, base and head commits, and full diff directly from GitHub.",
        {"number": NUMBER},
        ["number"],
    ),
    "read_pull_request_file": (
        read_pull_request_file,
        "Read a text file from the exact head commit returned by read_pull_request.",
        {"number": NUMBER, "head_sha": {"type": "string"}, "path": {"type": "string"}},
        ["number", "head_sha", "path"],
    ),
}


def tool_definitions():
    return [
        {"name": name, "description": f"{description} Repository: {HOST}/{REPOSITORY}.",
         "inputSchema": {"type": "object", "properties": properties, "required": required,
                         "additionalProperties": False},
         "annotations": {"readOnlyHint": True, "destructiveHint": False, "openWorldHint": True}}
        for name, (_, description, properties, required) in TOOLS.items()
    ]


def call_tool(params):
    try:
        name = params.get("name")
        if not isinstance(name, str) or name not in TOOLS:
            raise ToolError("This server offers only the listed read-only GitHub tools.")
        action, _, properties, required = TOOLS[name]
        arguments = params.get("arguments", {})
        if (not isinstance(arguments, dict) or set(arguments) - properties.keys()
                or set(required) - arguments.keys()):
            raise ToolError("Arguments must match the published tool schema.")
        return {"content": [{"type": "text", "text": action(arguments)}]}
    except ToolError as exc:
        message = str(exc)
    except (KeyError, TypeError, ValueError):
        message = "GitHub returned data this tool could not read as text."
    return {"isError": True, "content": [{"type": "text", "text": message}]}


def respond(request):
    method = request.get("method")
    params = request.get("params", {})
    reply = {"jsonrpc": "2.0", "id": request.get("id")}
    if not isinstance(params, dict):
        return {**reply, "error": {"code": -32602, "message": "Invalid parameters"}}
    if method == "initialize":
        version = params.get("protocolVersion")
        result = {"protocolVersion": version if isinstance(version, str) and version in PROTOCOL_VERSIONS else "2025-06-18",
                  "capabilities": {"tools": {}},
                  "serverInfo": {"name": "github-review", "version": "1.0.0"}}
    elif method == "tools/list":
        result = {"tools": tool_definitions()}
    elif method == "tools/call":
        result = call_tool(params)
    elif method == "ping":
        result = {}
    else:
        return {**reply, "error": {"code": -32601, "message": "Method not found"}}
    return {**reply, "result": result}


if __name__ == "__main__":
    for line in sys.stdin:
        try:
            request = json.loads(line)
        except ValueError:
            response = {"jsonrpc": "2.0", "id": None, "error": {"code": -32700, "message": "Parse error"}}
        else:
            if not isinstance(request, dict) or request.get("jsonrpc") != "2.0" or "method" not in request:
                response = {"jsonrpc": "2.0", "id": None, "error": {"code": -32600, "message": "Invalid request"}}
            elif "id" not in request:
                continue
            else:
                response = respond(request)
        print(json.dumps(response, ensure_ascii=True), flush=True)
