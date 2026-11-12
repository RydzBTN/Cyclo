using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Cyclo.Api.Pagination;

public static class ActivityCursor
{
    public static string Encode(DateTimeOffset uploadedAt, Guid id)
    {
        string dateString = uploadedAt.ToString("O", CultureInfo.InvariantCulture);
        string cursor = $"{dateString}|{id}";
        
        byte[] bytes = Encoding.UTF8.GetBytes(cursor);

        return Base64Url.EncodeToString(bytes);
    }
    
    public static bool TryDecode(string encodedCursor, out DateTimeOffset uploadedAt, out Guid id)
    {
        uploadedAt = DateTimeOffset.MinValue;
        id = Guid.Empty;
        
        byte[] bytes;
        try
        {
            bytes = Base64Url.DecodeFromChars(encodedCursor);
        }
        catch (FormatException)
        {
            return false;
        }
        
        string cursor = Encoding.UTF8.GetString(bytes);

        string[] elements = cursor.Split("|");
        if (elements.Length != 2) return false;

        if (!DateTimeOffset.TryParseExact(elements[0], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out uploadedAt))
            return false;

        if (!Guid.TryParse(elements[1], out id)) return false;

        return true;
    }
}