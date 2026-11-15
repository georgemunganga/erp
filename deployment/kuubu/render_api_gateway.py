"""Render a fail-closed mobile edge beside the existing ERP proxy.

Only explicit installation configuration is accepted. Native mobile admission is
deliberately held until its client, worker scope and retry behavior are qualified.
"""
import argparse
import json
import re
from urllib.parse import urlsplit


def render(host, upstream, canonical_host, trust_file, issuer, native=False):
    for value in (host, canonical_host):
        if not re.fullmatch(r"[a-z0-9]+(?:[.-][a-z0-9]+)+", value):
            raise ValueError("A DNS hostname without path or port is required")
    target = urlsplit(upstream)
    if target.scheme != "https" or target.hostname != "127.0.0.1" or not target.port or target.path or target.query or target.fragment or target.username:
        raise ValueError("An explicit verified HTTPS loopback upstream is required")
    if not re.fullmatch(r"/[A-Za-z0-9_./-]+", trust_file) or ".." in trust_file.split("/"):
        raise ValueError("An absolute trust-file path is required")
    auth = urlsplit(issuer)
    if auth.scheme != "https" or not auth.hostname or auth.username or auth.query or auth.fragment or auth.path not in ("", "/"):
        raise ValueError("An HTTPS identity origin is required")
    if native:
        return render_native(host, upstream, canonical_host, trust_file)
    discovery = json.dumps({
        "schema_version": 1, "api_base_url": "https://" + host + "/v1",
        "identity_origin": issuer.rstrip("/"), "mobile_ready": False,
        "mobile_status": "native-client-and-employee-admission-pending",
        "modules": [{"id": "hrm", "base_path": "/v1/hrm", "attendance_ready": False}],
    }, separators=(",", ":"))
    error = lambda code: json.dumps(json.dumps({"error": code}, separators=(",", ":")))
    # Gate before dispatch: existing SaaS browser/admin admission must not be
    # advertised as native staff authorization. Infrastructure readiness is separate.
    return f'''{host} {{
    header {{
        -Server
        X-Content-Type-Options nosniff
        Referrer-Policy no-referrer
        Cache-Control "no-store"
        X-Kuubu-API-Version "1"
    }}
    request_body {{
        max_size 64KB
    }}
    @discovery {{
        method GET
        path / /.well-known/kuubu-api
    }}
    handle @discovery {{
        header Content-Type application/json
        respond {json.dumps(discovery)} 200
    }}
    @health {{
        method GET
        path /health/ready /health/live
    }}
    handle @health {{
        reverse_proxy {upstream} {{
            header_up Host {canonical_host}
            header_up -Cookie
            header_up -Authorization
            header_up -X-Forwarded-Host
            header_down -Set-Cookie
            transport http {{
                tls_server_name {canonical_host}
                tls_trust_pool file {trust_file}
                dial_timeout 3s
                response_header_timeout 10s
            }}
        }}
    }}
    @attendance path /v1/hrm/me/attendance /v1/hrm/me/attendance/today /v1/hrm/me/attendance/clock-in /v1/hrm/me/attendance/clock-out
    handle @attendance {{
        header Content-Type application/json
        @cookie header Cookie *
        respond @cookie {error('browser-cookie-not-supported')} 403
        @scope header_regexp injected_scope X-Tenant ".+"
        respond @scope {error('untrusted-scope-header')} 400
        @anonymous not header Authorization "Bearer *"
        header @anonymous WWW-Authenticate "Bearer realm=kuubu-mobile"
        respond @anonymous {error('authentication-required')} 401
        respond {error('mobile-admission-not-configured')} 503
    }}
    handle {{
        header Content-Type application/json
        respond {error('api-route-not-found')} 404
    }}
}}
'''


def render_native(host, upstream, canonical_host, trust_file):
    error = lambda code: json.dumps(json.dumps({"error": code}, separators=(",", ":")))
    proxy = f'''reverse_proxy {upstream} {{
            header_up Host {canonical_host}
            header_up -X-Forwarded-Host
            header_down -Set-Cookie
            transport http {{
                tls_server_name {canonical_host}
                tls_trust_pool file {trust_file}
                dial_timeout 3s
                response_header_timeout 30s
            }}
        }}'''
    return f'''{host} {{
    header {{
        -Server
        X-Content-Type-Options nosniff
        Referrer-Policy no-referrer
        Cache-Control "no-store"
        X-Kuubu-API-Version "1"
    }}
    request_body {{
        max_size 8KB
    }}
    @discovery {{
        method GET
        path / /.well-known/kuubu-api
    }}
    handle @discovery {{
        rewrite * /api/mobile/v1/config
        {proxy}
    }}
    @health {{
        method GET
        path /health/ready /health/live
    }}
    handle @health {{
        {proxy}
    }}
    @nativeRead {{
        method GET
        path /v1/companies /v1/hrm/me /v1/hrm/me/attendance /v1/hrm/me/attendance/today
    }}
    @nativeWrite {{
        method POST
        path /v1/hrm/me/attendance/clock-in /v1/hrm/me/attendance/clock-out
    }}
    handle @nativeRead {{
        route {{
            @cookieRead header Cookie *
            respond @cookieRead {error('browser-cookie-not-supported')} 403
            @tenantRead header X-Tenant *
            respond @tenantRead {error('untrusted-scope-header')} 400
            rewrite * /api/mobile{{uri}}
            {proxy}
        }}
    }}
    handle @nativeWrite {{
        route {{
            @cookieWrite header Cookie *
            respond @cookieWrite {error('browser-cookie-not-supported')} 403
            @tenantWrite header X-Tenant *
            respond @tenantWrite {error('untrusted-scope-header')} 400
            rewrite * /api/mobile{{uri}}
            {proxy}
        }}
    }}
    handle {{
        header Content-Type application/json
        respond {error('api-route-not-found')} 404
    }}
}}
'''


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--host', required=True)
    p.add_argument('--upstream', required=True)
    p.add_argument('--canonical-host', required=True)
    p.add_argument('--trust-file', required=True)
    p.add_argument('--issuer', required=True)
    p.add_argument('--native', action='store_true', help='Enable only after the registered native profile passes employee/attendance acceptance')
    a = p.parse_args()
    print(render(a.host, a.upstream, a.canonical_host, a.trust_file, a.issuer, a.native), end='')
