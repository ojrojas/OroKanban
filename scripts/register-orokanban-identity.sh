#!/usr/bin/env bash
# Registers OroKanban OIDC clients, scopes, roles and seed users in OroIdentityServer
# via its master-admin API (draft/oroidentityserver-specification.md § API Endpoints).
#
# Idempotent — safe to run repeatedly. Existing entities are detected via GET before POST.
#
# Usage:
#   IDP_URL=https://localhost:5086 IDP_ADMIN_USER=admin IDP_ADMIN_PASSWORD='...' \
#   WEB_REDIRECT_URI=http://localhost:4200/auth/callback \
#   WEB_POST_LOGOUT_URI=http://localhost:4200/auth/logout-callback \
#   WEB_CLIENT_SECRET="$(openssl rand -hex 32)" \
#   ./scripts/register-orokanban-identity.sh
#
# The printed WEB_CLIENT_SECRET / ADMIN_CLIENT_SECRET must be stored as Aspire
# parameters `orokanban-web-secret` / `orokanban-admin-secret` (AppHost user secrets)
# or `Identity__ClientSecret` env vars (see draft/oroidentityserver-specification.md
# § Configuration and contracts/identity-config-contract.md).
#
# Requires: curl, jq (optional for pretty logs)
set -euo pipefail

# ---------------------------------------------------------------------------
# Config (environment overrides)
# ---------------------------------------------------------------------------
IDP_URL="${IDP_URL:-http://localhost:5080}"
IDP_ADMIN_USER="${IDP_ADMIN_USER:-admin}"
IDP_ADMIN_PASSWORD="${IDP_ADMIN_PASSWORD:-Admin@123456}"
if [ "$IDP_ADMIN_PASSWORD" = "Admin@123456" ]; then
  echo "ℹ️  Usando IDP_ADMIN_PASSWORD por defecto 'Admin@123456' (seed de oroidentityserver). Para producción, exporta IDP_ADMIN_PASSWORD con el password real."
fi

# Clients
WEB_CLIENT_ID="${WEB_CLIENT_ID:-orokanban-web}"
WEB_CLIENT_SECRET="${WEB_CLIENT_SECRET:-}" # empty = public client (PKCE, no secret). Set to non-empty for confidential.
WEB_REDIRECT_URI="${WEB_REDIRECT_URI:-http://localhost:4200/auth/callback}"
WEB_POST_LOGOUT_URI="${WEB_POST_LOGOUT_URI:-http://localhost:4200/auth/logout-callback}"
WEB_SILENT_RENEW_URI="${WEB_SILENT_RENEW_URI:-http://localhost:4200/silent-renew.html}"

ADMIN_CLIENT_ID="${ADMIN_CLIENT_ID:-orokanban-admin}"
ADMIN_REDIRECT_URI="${ADMIN_REDIRECT_URI:-https://localhost:7172/signin-oidc}"
ADMIN_POST_LOGOUT_URI="${ADMIN_POST_LOGOUT_URI:-https://localhost:7172/signout-callback-oidc}"
ADMIN_CLIENT_SECRET="${ADMIN_CLIENT_SECRET:-$(openssl rand -hex 32 2>/dev/null || echo "dev-admin-secret-$(date +%s)")}"

API_CLIENT_ID="${API_CLIENT_ID:-orokanban-api-client}"
API_CLIENT_SECRET="${API_CLIENT_SECRET:-$(openssl rand -hex 32 2>/dev/null || echo "dev-api-secret-$(date +%s)")}"

# Scopes / Roles / Tenant
API_SCOPE="${API_SCOPE:-orokanban-api}"
TENANT_NAME="${TENANT_NAME:-OroMasterTenant}"

cookies="$(mktemp)"
trap 'rm -f "$cookies"' EXIT

have_jq=false; command -v jq >/dev/null 2>&1 && have_jq=true
log_json() { if $have_jq; then echo "$1" | jq . 2>/dev/null || echo "$1"; else echo "$1"; fi; }

# ---------------------------------------------------------------------------
# Helpers — all curl calls use -L to follow 307 http→https redirects (identity-api behind Aspire proxy)
# ---------------------------------------------------------------------------
api_code() { curl -Lsk -b "$cookies" -o /tmp/resp.json -w "%{http_code}" "$@"; }
api_get_code() { api_code -H "Content-Type: application/json" -X GET "$1"; }

ensure_scope() {
  local name="$1" display="$2" desc="$3"
  # Check existence via list (GET /api/scopes/{name} is 405, so list all)
  local exists
  exists=$(curl -Lsk -b "$cookies" "$IDP_URL/api/scopes" 2>/dev/null | grep -o "\"Name\":\"$name\"" | head -1 || true)
  if [ -n "$exists" ]; then
    echo "   scope '$name' already exists — skip"
    return 0
  fi
  # Fallback check via GET by name (some deployments support it)
  local code
  code=$(api_get_code "$IDP_URL/api/scopes/$name" 2>/dev/null || echo "000")
  if [ "$code" = "200" ]; then
    echo "   scope '$name' already exists — skip"
    return 0
  fi
  echo "   creating scope '$name'"
  local sc
  # CreateScopeCommand expects { Name, Resources } — Resources must not be null (see CreateScopeCommandHandler.cs:36 foreach Resources)
  sc=$(api_code -X POST "$IDP_URL/api/scopes" -H "Content-Type: application/json" -d "{
    \"Name\": \"$name\",
    \"Resources\": [\"$name\"]
  }")
  echo "   scope $name: HTTP $sc"
  if [ "$sc" -ge 400 ] && $have_jq; then log_json "$(cat /tmp/resp.json)"; fi
}

ensure_role() {
  local name="$1" desc="$2"
  # List and check if exists (API has no GET by name for roles, so we list — handles both PascalCase and camelCase)
  local exists
  exists=$(curl -Lsk -b "$cookies" "$IDP_URL/api/roles" 2>/dev/null | grep -o "\"[Nn]ame\":\"$name\"" | head -1 || true)
  if [ -n "$exists" ]; then
    echo "   role '$name' already exists — skip"
    return 0
  fi
  echo "   creating role '$name'"
  local sc
  # CreateRoleCommand expects PascalCase { Name, Description } — see Roles table NotNull constraint on "Name"
  # Send both casings for compatibility, and include NormalizedName implicitly handled server-side
  sc=$(api_code -X POST "$IDP_URL/api/roles" -H "Content-Type: application/json" -d "{
    \"Name\": \"$name\",
    \"name\": \"$name\",
    \"Description\": \"$desc\",
    \"description\": \"$desc\"
  }")
  echo "   role $name: HTTP $sc"
  if [ "$sc" -ge 400 ] && $have_jq; then log_json "$(cat /tmp/resp.json)"; fi
}

ensure_user() {
  local username="$1" email="$2" firstName="$3" lastName="$4" password="$5" identification="$6"
  # Check by listing users and grep (handles both camelCase and PascalCase)
  local exists
  exists=$(curl -Lsk -b "$cookies" "$IDP_URL/api/users" 2>/dev/null | grep -o "\"[Uu]serName\":\"$username\"" | head -1 || true)
  if [ -n "$exists" ]; then
    echo "   user '$username' already exists — skip"
    # Try to fetch id for role assignment
    curl -Lsk -b "$cookies" "$IDP_URL/api/users" 2>/dev/null | jq -r ".[] | select(.userName==\"$username\" or .UserName==\"$username\") | .id // .Id // empty" 2>/dev/null | head -1 || true
    return 0
  fi
  echo "   creating user '$username' ($email)"
  # Resolve tenant and identification type for CreateUserRequest (requires TenantId and IdentificationTypeId as Guid)
  local tenant_id_for_user
  tenant_id_for_user=$(curl -Lsk -b "$cookies" "$IDP_URL/api/tenants" 2>/dev/null | jq -r ".[] | select(.name==\"$TENANT_NAME\" or .Name==\"$TENANT_NAME\") | .id // .Id // empty" 2>/dev/null | head -1 || echo "")
  if [ -z "$tenant_id_for_user" ]; then
    # Fallback to OroMasterRealm (seed default) if OroMasterTenant not found
    tenant_id_for_user=$(curl -Lsk -b "$cookies" "$IDP_URL/api/tenants" 2>/dev/null | jq -r '.[0] | .id // .Id // empty' 2>/dev/null | head -1 || echo "")
  fi
  local idtype_id
  idtype_id=$(curl -Lsk -b "$cookies" "$IDP_URL/api/identification-types" 2>/dev/null | jq -r '.[] | select(.code=="CC" or .Code=="CC" or .name=="CC" or .Name=="CC") | .id // .Id // empty' 2>/dev/null | head -1 || echo "")
  if [ -z "$idtype_id" ]; then
    # Fallback: list and take first
    idtype_id=$(curl -Lsk -b "$cookies" "$IDP_URL/api/identification-types" 2>/dev/null | jq -r '.[0] | .id // .Id // empty' 2>/dev/null | head -1 || echo "")
  fi
  if [ -z "$tenant_id_for_user" ]; then
    echo "   !! tenant id not found for user creation — will try without TenantId (may fail)"
    tenant_id_for_user="00000000-0000-0000-0000-000000000000"
  fi
  if [ -z "$idtype_id" ]; then
    echo "   !! identificationTypeId not found — will try without (may fail)"
    idtype_id="00000000-0000-0000-0000-000000000000"
  fi
  local sc
  # CreateUserRequest expects PascalCase: Name, MiddleName, LastName, UserName, Email, Password, Identification, IdentificationTypeId, TenantId
  sc=$(api_code -X POST "$IDP_URL/api/users" -H "Content-Type: application/json" -d "{
    \"Name\": \"$firstName\",
    \"MiddleName\": \"\",
    \"LastName\": \"$lastName\",
    \"UserName\": \"$username\",
    \"Email\": \"$email\",
    \"Password\": \"$password\",
    \"Identification\": \"$identification\",
    \"IdentificationTypeId\": \"$idtype_id\",
    \"TenantId\": \"$tenant_id_for_user\"
  }")
  echo "   user $username: HTTP $sc"
  if $have_jq; then log_json "$(cat /tmp/resp.json)"; fi
  # Return id if created
  if [ "$sc" -ge 200 ] && [ "$sc" -lt 300 ] && $have_jq; then
    cat /tmp/resp.json | jq -r '.id // .Id // .userId // .UserId // empty' 2>/dev/null | head -1 || true
  fi
}

assign_role_to_user() {
  local userId="$1" roleName="$2"
  if [ -z "$userId" ] || [ -z "$roleName" ]; then return 0; fi
  echo "   assigning role '$roleName' to user $userId"
  local sc
  # Resolve role id
  local roleId
  roleId=$(curl -Lsk -b "$cookies" "$IDP_URL/api/roles" 2>/dev/null | $have_jq && curl -Lsk -b "$cookies" "$IDP_URL/api/roles" 2>/dev/null | jq -r ".[] | select(.name==\"$roleName\") | .id" 2>/dev/null | head -1 || echo "")
  if [ -z "$roleId" ]; then
    echo "   !! role '$roleName' not found — skip assignment"
    return 0
  fi
  sc=$(api_code -X PUT "$IDP_URL/api/users/$userId/roles" -H "Content-Type: application/json" -d "{
    \"roleIds\": [\"$roleId\"]
  }")
  echo "   assign $roleName -> $userId: HTTP $sc"
}

# ---------------------------------------------------------------------------
# 1. Sign in as master admin (cookie + is_master_admin claim)
# ---------------------------------------------------------------------------
echo "-> Signing in to $IDP_URL as $IDP_ADMIN_USER"
curl -Lsk -c "$cookies" -o /dev/null -w "   login: HTTP %{http_code}\n" \
  -X POST "$IDP_URL/auth/login" \
  --data-urlencode "loginIdentifier=$IDP_ADMIN_USER" \
  --data-urlencode "password=$IDP_ADMIN_PASSWORD"

# Verify we got a session cookie
if ! grep -q "Cookie" "$cookies" 2>/dev/null && ! grep -q "AspNetCore" "$cookies" 2>/dev/null; then
  echo "!! No session cookie after login — check IDP_ADMIN_USER / IDP_ADMIN_PASSWORD and that identity-api is reachable at $IDP_URL"
  echo "   Try: curl -Lsk $IDP_URL/.well-known/openid-configuration | jq .issuer"
fi

# ---------------------------------------------------------------------------
# 2. Scopes (MasterAdminOnly — requires is_master_admin)
# ---------------------------------------------------------------------------
echo ""
echo "-> Ensuring scopes"
ensure_scope "$API_SCOPE" "OroKanban API" "Resource scope for OroKanban Api — required audience orokanban-api"
ensure_scope "admin" "Admin" "Admin API scope"
ensure_scope "roles" "Roles" "Roles claim scope (scp:roles)"

# ---------------------------------------------------------------------------
# 3. Roles (AdminOnly)
# ---------------------------------------------------------------------------
if [ "${SKIP_ROLES:-false}" = "true" ]; then
  echo ""
  echo "-> Skipping roles (SKIP_ROLES=true)"
else
echo ""
echo "-> Ensuring roles (10 required by spec 002 / constitution)"
ensure_role "RootManager" "Top of hierarchy — chief"
ensure_role "Manager" "Manages other managers and employees"
ensure_role "Supervisor" "Manages contributors"
ensure_role "Contributor" "Work item contributor"
ensure_role "Reviewer" "Reviews work items"
ensure_role "Auditor" "Read-only audit access"
ensure_role "DocumentManager" "Manages documents"
ensure_role "ProjectManager" "Manages projects"
ensure_role "AIReviewer" "Reviews AI-generated content"
ensure_role "Administrator" "Full administrator (is_master_admin seed)"
fi

# ---------------------------------------------------------------------------
# 4. Tenant (MasterAdminOnly) — skipped if SKIP_TENANT=true
# ---------------------------------------------------------------------------
if [ "${SKIP_TENANT:-false}" = "true" ]; then
  echo ""
  echo "-> Skipping tenant (SKIP_TENANT=true)"
else
echo ""
echo "-> Ensuring tenant '$TENANT_NAME'"
# Tenant API expects CreateTenantRequest { Name, Slug, OwnerId } — fetch admin user Id as owner
admin_id_for_tenant=$(curl -Lsk -b "$cookies" "$IDP_URL/api/users" 2>/dev/null | jq -r ".[] | select(.userName==\"$IDP_ADMIN_USER\" or .UserName==\"$IDP_ADMIN_USER\") | .id // .Id // empty" 2>/dev/null | head -1 || echo "")
if [ -z "$admin_id_for_tenant" ]; then
  # Fallback: try to get current user via /api/users/me or assume admin is first user
  admin_id_for_tenant=$(curl -Lsk -b "$cookies" "$IDP_URL/api/users" 2>/dev/null | jq -r '.[0] | .id // .Id // empty' 2>/dev/null | head -1 || echo "00000000-0000-0000-0000-000000000000")
fi
slug=$(echo "$TENANT_NAME" | tr '[:upper:]' '[:lower:]' | tr -cd 'a-z0-9' | head -c 30)
if echo "$(cat /tmp/resp.json 2>/dev/null)" | grep -q "\"[Nn]ame\":\"$TENANT_NAME\"" 2>/dev/null; then
  echo "   tenant '$TENANT_NAME' already exists — skip"
else
  # Also check via list with case-insensitive
  existing_tenant=$(curl -Lsk -b "$cookies" "$IDP_URL/api/tenants" 2>/dev/null | grep -o "\"[Nn]ame\":\"$TENANT_NAME\"" | head -1 || true)
  if [ -n "$existing_tenant" ]; then
    echo "   tenant '$TENANT_NAME' already exists — skip"
  else
    echo "   creating tenant '$TENANT_NAME' (slug: $slug, owner: $admin_id_for_tenant)"
    sc=$(api_code -X POST "$IDP_URL/api/tenants" -H "Content-Type: application/json" -d "{
      \"Name\": \"$TENANT_NAME\",
      \"Slug\": \"$slug\",
      \"OwnerId\": \"$admin_id_for_tenant\"
    }")
    echo "   tenant $TENANT_NAME: HTTP $sc"
    if $have_jq; then log_json "$(cat /tmp/resp.json)"; fi
    if [ "$sc" -ge 400 ]; then
      echo "   !! tenant creation failed — will try with name only as fallback"
      sc=$(api_code -X POST "$IDP_URL/api/tenants" -H "Content-Type: application/json" -d "{
        \"Name\": \"$TENANT_NAME\",
        \"Slug\": \"$slug\",
        \"OwnerId\": \"$admin_id_for_tenant\",
        \"name\": \"$TENANT_NAME\",
        \"slug\": \"$slug\"
      }")
      echo "   tenant $TENANT_NAME (fallback): HTTP $sc"
      if $have_jq; then log_json "$(cat /tmp/resp.json)"; fi
    fi
  fi
fi
fi

# ---------------------------------------------------------------------------
# 5. Users (ManagerOrAdmin) — skipped if SKIP_USERS=true (user creates users manually)
# ---------------------------------------------------------------------------
if [ "${SKIP_USERS:-true}" = "true" ]; then
  echo ""
  echo "-> Skipping users (SKIP_USERS=true — los usuarios los creo yo)"
else
echo ""
echo "-> Ensuring seed users"
# The seeded admin 'admin' already exists (SEED_ADMIN_*). Create test users for hierarchy validation.
# Passwords are dev-only; all except admin will be forced to change on first login (must_change_password claim).
alice_id=$(ensure_user "alice.manager" "alice.manager@orokanban.local" "Alice" "Manager" "Manager@123456" "100000001")
bob_id=$(ensure_user "bob.contributor" "bob.contributor@orokanban.local" "Bob" "Contributor" "Contributor@123456" "100000002")
carol_id=$(ensure_user "carol.auditor" "carol.auditor@orokanban.local" "Carol" "Auditor" "Auditor@123456" "100000003")

# Assign roles (need to resolve ids — ensure_user may have returned empty if already existed, so re-fetch)
resolve_user_id() {
  curl -Lsk -b "$cookies" "$IDP_URL/api/users" 2>/dev/null | jq -r ".[] | select(.userName==\"$1\" or .UserName==\"$1\") | .id // .Id // empty" 2>/dev/null | head -1
}
if [ -z "$alice_id" ]; then alice_id=$(resolve_user_id "alice.manager"); fi
if [ -z "$bob_id" ]; then bob_id=$(resolve_user_id "bob.contributor"); fi
if [ -z "$carol_id" ]; then carol_id=$(resolve_user_id "carol.auditor"); fi

assign_role_to_user "$alice_id" "Manager"
assign_role_to_user "$bob_id" "Contributor"
assign_role_to_user "$carol_id" "Auditor"

# Optionally add users to tenant (if API requires explicit tenant assignment)
# POST /api/tenants/{id}/users — best-effort, ignore 404/409 — expects { UserId: Guid }
if [ -n "$alice_id" ]; then
  tenant_id=$(curl -Lsk -b "$cookies" "$IDP_URL/api/tenants" 2>/dev/null | jq -r ".[] | select(.name==\"$TENANT_NAME\" or .Name==\"$TENANT_NAME\") | .id // .Id // empty" 2>/dev/null | head -1 || echo "")
  if [ -n "$tenant_id" ]; then
    for uid in "$alice_id" "$bob_id" "$carol_id"; do
      if [ -n "$uid" ]; then
        sc=$(api_code -X POST "$IDP_URL/api/tenants/$tenant_id/users" -H "Content-Type: application/json" -d "{\"UserId\": \"$uid\", \"userId\": \"$uid\"}" 2>/dev/null || echo "000")
        echo "   tenant $TENANT_NAME <- user $uid: HTTP $sc"
      fi
    done
  fi
fi
fi

# ---------------------------------------------------------------------------
# 6. Applications / OIDC clients (MasterAdminOnly)
# ---------------------------------------------------------------------------
echo ""
echo "-> Checking existing OIDC clients"

register_client() {
  local client_id="$1" secret="$2" display="$3" ctype="$4" atype="$5" redirect="$6" post_logout="$7" extra_perms="$8" extra_grants="$9"
  local status
  status=$(curl -Lsk -b "$cookies" -o /dev/null -w "%{http_code}" "$IDP_URL/api/applications/$client_id" 2>/dev/null || echo "000")
  if [ "$status" = "200" ]; then
    echo "   client '$client_id' already exists — skip (borra el volumen para recrear limpio)"
    return 0
  fi
  echo "   creating client '$client_id' ($display)"
  local perms="\"ept:authorization\", \"ept:token\", \"ept:end_session\", \"ept:userinfo\", \"rst:code\""
  if [ -n "$extra_perms" ]; then perms="$perms, $extra_perms"; fi
  local grants="\"gt:authorization_code\", \"gt:refresh_token\""
  if [ -n "$extra_grants" ]; then grants="$grants, $extra_grants"; fi
  local all_perms="$perms, $grants"
  local method="POST"
  local url="$IDP_URL/api/applications"
  local body="{
    \"clientId\": \"$client_id\",
    \"displayName\": \"$display\",
    \"clientType\": \"$ctype\",
    \"applicationType\": \"$atype\",
    \"consentType\": \"implicit\",
    \"permissions\": [$all_perms],
    \"requirements\": [\"ft:pkce\"],
    \"redirectUris\": [\"$redirect\"],
    \"postLogoutRedirectUris\": [\"$post_logout\"]"
  if [ -n "$secret" ]; then
    body="$body,
    \"clientSecret\": \"$secret\""
  fi
  body="$body
  }"
  local sc
  sc=$(curl -Lsk -b "$cookies" -H "Content-Type: application/json" -o /tmp/resp.json -w "%{http_code}" -X "$method" "$url" -d "$body" 2>/dev/null || echo "000")
  echo "   client $client_id: HTTP $sc"
  if $have_jq; then log_json "$(cat /tmp/resp.json)"; else cat /tmp/resp.json 2>/dev/null | head -n 20; fi
}

# Web (public, PKCE) — angular-auth-oidc-client, silentRenew + useRefreshToken
register_client "$WEB_CLIENT_ID" "$WEB_CLIENT_SECRET" "OroKanban Web (Angular)" "public" "web" "$WEB_REDIRECT_URI" "$WEB_POST_LOGOUT_URI" "\"scp:openid\", \"scp:profile\", \"scp:email\", \"scp:roles\", \"scp:offline_access\", \"scp:orokanban-api\"" "\"gt:password\""

# Also ensure silent-renew redirect is allowed (some deployments check exact redirectUris)
# If the client was just created, patch it to add the silent-renew URI as an extra redirect
if [ -n "$WEB_SILENT_RENEW_URI" ] && [ "$WEB_SILENT_RENEW_URI" != "$WEB_REDIRECT_URI" ]; then
  echo "   (web silent-renew URI $WEB_SILENT_RENEW_URI — ensure it is added via PUT if validation fails; current spec only allows one redirectUris entry in this script)"
fi

# Admin BFF (confidential, web, PKCE) — for server-side admin if needed (mirrors quizarena-admin example)
register_client "$ADMIN_CLIENT_ID" "$ADMIN_CLIENT_SECRET" "OroKanban Administration (BFF)" "confidential" "web" "$ADMIN_REDIRECT_URI" "$ADMIN_POST_LOGOUT_URI" "\"scp:openid\", \"scp:profile\", \"scp:email\", \"scp:roles\", \"scp:offline_access\", \"scp:admin\", \"scp:orokanban-api\"" ""

# API client (confidential, for service-to-service via client_credentials + password for hello-world curl)
register_client "$API_CLIENT_ID" "$API_CLIENT_SECRET" "OroKanban API client (service)" "confidential" "web" "http://localhost:5000/callback" "http://localhost:5000/logout-callback" "\"scp:orokanban-api\"" "\"gt:client_credentials\", \"gt:password\""

echo ""
echo "-> Done."
echo ""
echo "   WEB_CLIENT_ID=$WEB_CLIENT_ID"
if [ -n "$WEB_CLIENT_SECRET" ]; then
  echo "   WEB_CLIENT_SECRET=$WEB_CLIENT_SECRET (store as orokanban-web-secret / Identity__ClientSecret if confidential)"
else
  echo "   WEB_CLIENT_ID is public (PKCE) — no secret needed (angular-auth-oidc-client silentRenew + refresh_token via code flow)"
fi
echo "   ADMIN_CLIENT_ID=$ADMIN_CLIENT_ID"
echo "   ADMIN_CLIENT_SECRET=$ADMIN_CLIENT_SECRET  <- store as orokanban-admin-secret (Aspire user secrets: dotnet user-secrets set \"Parameters:symmetric-security-key\" etc. — see AppHost)"
echo "   API_CLIENT_ID=$API_CLIENT_ID"
echo "   API_CLIENT_SECRET=$API_CLIENT_SECRET  <- for hello-world password-flow curl"
echo ""
echo "   Test hello-world (password flow, no browser):"
echo "   curl -X POST $IDP_URL/connect/token -H \"Content-Type: application/x-www-form-urlencoded\" \\"
echo "     -d \"grant_type=password&username=alice.manager&password=Manager@123456&client_id=$WEB_CLIENT_ID&scope=openid%20profile%20email%20roles%20offline_access%20orokanban-api\" | jq .access_token"
echo "   curl -H \"Authorization: Bearer <access_token>\" http://localhost:5000/api/hello | jq ."
echo ""
echo "   Or via Angular Web: npm start in src/Web (ng serve) -> login as alice.manager / Manager@123456, then fetch /api/hello via authInterceptor."
