namespace AutomationEngine.Domain.Interfaces;

public interface IDocumentSerializer
{
    /// <summary>
    /// Serializes markdown/plain-text content into a .docx byte array.
    /// </summary>
    byte[] SerializeToDocx(string content);
}
