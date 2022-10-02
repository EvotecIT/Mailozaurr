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
		try {
			$FileStream = [System.IO.StreamReader]::new($Attachment)
			$FileSize = $FileStream.BaseStream.Length
			$FileName = [System.IO.Path]::GetFileName($Attachment)
		} catch {
			if ($PSBoundParameters.ErrorAction -eq 'Stop') {
				throw
			} else {
				Write-Warning "New-GraphAttachment - Error reading attachment: $($_.Exception.Message) $ErrorDetails"
			}
			continue
		}
		$File = @{
			"AttachmentItem" = [ordered] @{
				"attachmentType" = "file"
				"name"           = $FileName
				"size"           = $FileSize
			}
		}
		$FileJson = $File | ConvertTo-Json -Depth 2

		try {
			$Results = Invoke-RestMethod -Method POST -Uri $UploadSession -Headers $Authorization -Body $FileJson -ContentType 'application/json; charset=UTF-8' -UserAgent "Mailozaurr" -ErrorAction Stop
		} catch {
			if ($PSBoundParameters.ErrorAction -eq 'Stop') {
				throw
			} else {
				Write-Warning "New-GraphAttachment - Error creating upload session: $($_.Exception.Message) $ErrorDetails"
			}
			continue
		}
		$UploadUrl = $Results.uploadUrl
		if ($UploadChunkSize -gt $FileStream.BaseStream.Length) {
			$UploadChunkSize = $FileStream.BaseStream.Length
		}
		$FileOffsetStart = 0
		$FileBuffer = [byte[]]::new($UploadChunkSize)
		do {
			$FileChunkByteCount = $FileStream.BaseStream.Read($FileBuffer, 0, $FileBuffer.Length)
			$FileOffsetEnd = $FileStream.BaseStream.Position - 1
			if ($FileChunkByteCount -gt 0) {
				$UploadRangeHeader = "bytes " + $FileOffsetStart + "-" + $FileOffsetEnd + "/" + $FileSize
				$FileOffsetStart = $FileStream.BaseStream.Position
				$BinaryContent = [System.Net.Http.ByteArrayContent]::new($FileBuffer, 0, $FileChunkByteCount)
				$FileBuffer = [byte[]]::new($UploadChunkSize)
				$Headers = @{
					"Content-Range"  = $UploadRangeHeader
					"AnrchorMailbox" = $FromField
				}
				try {
					$Results = Invoke-RestMethod -Method PUT -Uri $UploadUrl -Headers $Headers -Body $BinaryContent.ReadAsByteArrayAsync().Result -ContentType "application/octet-stream" -UserAgent "Mailozaurr" -Verbose:$false
				} catch {
					if ($PSBoundParameters.ErrorAction -eq 'Stop') {
						throw
					} else {
						Write-Warning "New-GraphAttachment - Error sending bytes to upload session: $($_.Exception.Message) $ErrorDetails"
					}
					break
				}
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