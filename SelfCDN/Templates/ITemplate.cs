using Shared.Model.Templates;

namespace SelfCDN.Templates
{
    public interface ITemplate
    {
        public string ToJson();
        public string ToHtml();
    }
}
