using System.Text.Json;
using OpenLethe.Server.Wire;
using Xunit;

public class UpdatedFormatSerializationTests
{
    [Fact]
    public void UnsetFields_AreOmitted_SetFieldsEmitted()
    {
        var updated = new UpdatedFormat { isInitialized = true };

        var json = JsonSerializer.Serialize(updated, global::PacketJson.Options);

        Assert.Equal("""{"isInitialized":true}""", json);
    }

    [Fact]
    public void ElementType_SerializesAsType_NotType_()
    {
        var mail = new InitializedMail
        {
            mail_id = 1,
            attachments = { new OpenLethe.Server.Wire.Element { type_ = "ITEM", id = 3, num = 50000 } },
        };

        var json = JsonSerializer.Serialize(mail, global::PacketJson.Options);

        Assert.Contains("\"type\":\"ITEM\"", json);
        Assert.DoesNotContain("type_", json);
    }
}
