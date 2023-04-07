using System.Threading.Tasks;
using System.Xml;

namespace Kevsoft.Ssml
{
    public interface ISsmlWriter
    {
        Task WriteAsync(XmlWriter writer);
    }
}