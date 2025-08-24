$sample = @'
POST https://graph.microsoft.com/v1.0/users/przemyslaw.klys@company.pl/sendMail
HTTP/2.0 404 Not Found
request-id: 2ff18766-1395-4fb9-abd1-162774d4b063
client-request-id: 6c57f9e6-3cad-48ee-8f7a-d566dc92aca3
x-ms-ags-diagnostic: {"ServerInfo":{"DataCenter":"Poland Central","Slice":"E","Ring":"2","ScaleUnit":"002","RoleInstance":"WA3PEPF000004A2"}}
Date: Sun, 24 Aug 2025 12:55:18 GMT
Content-Type: application/json; odata.metadata=minimal; odata.streaming=true; IEEE754Compatible=false; charset=utf-8

{"error":{"code":"ErrorInvalidUser","message":"The requested user 'przemyslaw.klys@company.pl' is invalid."}}
'

$parsed = [Mailozaurr.GraphApiErrorParser]::Parse($sample)
$parsed.Headers.Diagnostic.ServerInfo
$parsed.Error.Error
