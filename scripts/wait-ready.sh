#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
python3 - <<'PYWAIT'
import time,urllib.request
from pathlib import Path
values=dict(line.split('=',1) for line in Path('.env').read_text().splitlines() if '=' in line)
base='http://localhost:'+values.get('APP_PORT','8080')
for attempt in range(120):
    try:
        for path in ['/health','/auth/health','/gis/health','/booking/health']:
            # Service health endpoints are deliberately authenticated at the gateway.
            if path!='/health': continue
            urllib.request.urlopen(base+path,timeout=2).read()
        payload=__import__('json').dumps({'username':'employee','password':values['DEMO_PASSWORD']}).encode()
        login=__import__('json').load(urllib.request.urlopen(urllib.request.Request(base+'/auth/login',payload,{'Content-Type':'application/json'}),timeout=3))
        for service in ['auth','gis','booking']:
            urllib.request.urlopen(urllib.request.Request(base+'/'+service+'/ready',headers={'Authorization':'Bearer '+login['accessToken']}),timeout=3).read()
        print('Taxi Workshop is ready at '+base)
        break
    except Exception:
        if attempt==119: raise SystemExit('Services did not become ready. Run ./scripts/compose.sh logs --tail=80')
        time.sleep(2)
PYWAIT
