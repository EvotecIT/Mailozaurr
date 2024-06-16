namespace Mailozaurr;

public class GraphAttachmentPlaceHolder {
    public string Json { get; set; }
    public List<ByteArrayContent> Content { get; set; }
    public long FileSize { get; set; }
    public string FileName { get; set; }
}

public class GraphAttachmentItemWrapper {
    [JsonPropertyName("AttachmentItem")]
    public GraphAttachmentItem AttachmentItem { get; set; }

    public GraphAttachmentItemWrapper(GraphAttachmentItem attachmentItem) {
        AttachmentItem = attachmentItem;
    }
}

public class GraphAttachmentItem {
    [JsonPropertyName("attachmentType")]
    public string AttachmentType { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    public GraphAttachmentItem(string attachmentType, string name, long size) {
        AttachmentType = attachmentType;
        Name = name;
        Size = size;
    }
}

public class GraphAttachment {
    [JsonPropertyName("@odata.type")]
    public string ODataType { get; set; } = "#microsoft.graph.fileAttachment";

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("contentBytes")]
    public string ContentBytes { get; set; }

    public static GraphAttachment FromFile(string filePath) {
        var fileInfo = new FileInfo(filePath);
        var fileBytes = File.ReadAllBytes(filePath);
        var fileContentBase64 = Convert.ToBase64String(fileBytes);

        return new GraphAttachment {
            Name = fileInfo.Name,
            ContentBytes = fileContentBase64
        };
    }
}