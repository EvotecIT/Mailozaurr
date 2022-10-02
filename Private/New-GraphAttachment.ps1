function New-GraphAttachment {
	[cmdletbinding()]
	param(
		[PSCustomObject] $DraftMessage,
		[string] $FromField,
		[System.Collections.IDictionary] $Authorization,
		[string[]] $Attachments
	)
	[Int32] $UploadChunkSize = 9000000 # 9MB chunks
	$Authorization['AnchorMailbox'] = $FromField
	foreach ($Attachment in $Attachments) {
		$StopWatchAttachment = [System.Diagnostics.Stopwatch]::StartNew()
		Write-Verbose -Message "New-GraphAttachment - Uploading attachment '$Attachment'"
		$UploadSession = "https://graph.microsoft.com/v1.0/users('" + $FromField + "')/messages/" + $DraftMessage.id + "/attachments/createUploadSession"
		$fileStream = [System.IO.StreamReader]::new($Attachment)
		$FileSize = $fileStream.BaseStream.Length
		$FileName = [System.IO.Path]::GetFileName($Attachment)
		$File = @{
			"AttachmentItem" = [ordered] @{
				"attachmentType" = "file"
				"name"           = $FileName
				"size"           = $FileSize
			}
		}
		$FileJson = $File | ConvertTo-Json -Depth 2

		$Results = Invoke-RestMethod -Method POST -Uri $UploadSession -Headers $Authorization -Body $FileJson -ContentType 'application/json; charset=UTF-8' -UserAgent "Mailozaurr"
		$UploadUrl = $Results.uploadUrl
		if ($UploadChunkSize -gt $fileStream.BaseStream.Length) {
			$UploadChunkSize = $fileStream.BaseStream.Length
		}
		$FileOffsetStart = 0
		$FileBuffer = [byte[]]::new($UploadChunkSize)
		do {
			$FileChunkByteCount = $fileStream.BaseStream.Read($FileBuffer, 0, $FileBuffer.Length)
			$FileOffsetEnd = $fileStream.BaseStream.Position - 1
			if ($FileChunkByteCount -gt 0) {
				$UploadRangeHeader = "bytes " + $FileOffsetStart + "-" + $FileOffsetEnd + "/" + $FileSize
				$FileOffsetStart = $fileStream.BaseStream.Position
				$BinaryContent = [System.Net.Http.ByteArrayContent]::new($FileBuffer, 0, $FileChunkByteCount)
				$FileBuffer = [byte[]]::new($UploadChunkSize)
				$Headers = @{
					"Content-Range"  = $UploadRangeHeader
					"AnrchorMailbox" = $FromField
				}
				$Results = Invoke-RestMethod -Method PUT -Uri $UploadUrl -Headers $Headers -Body $BinaryContent.ReadAsByteArrayAsync().Result -ContentType "application/octet-stream" -UserAgent "Mailozaurr" -Verbose:$false
			}

		} while ($FileChunkByteCount -ne 0)

		$Authorization.Remove('AnchorMailbox')
		$StopWatchAttachment.Stop()
		Write-Verbose -Message "New-GraphAttachment - Attachment '$Attachment' uploaded in $($StopWatchAttachment.Elapsed.TotalSeconds) seconds"
	}
}

<#
POST https://graph.microsoft.com/v1.0/me/messages/AAMkAGUwNjQ4ZjIxLTQ3Y2YtNDViMi1iZjc4LTMA=/attachments/createUploadSession
Content-type: application/json

{
  "AttachmentItem": {
    "attachmentType": "file",
    "name": "scenary",
    "size": 7208534,
    "isInline": true,
    "contentId": "my_inline_picture"
  }
}
#>