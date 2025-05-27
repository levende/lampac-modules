using Shared.Model.Templates;

namespace SelfCDN.Templates
{
    public class SeasonTplWrapper : ITemplate
    {
        private readonly SeasonTpl _target;

        public SeasonTplWrapper(SeasonTpl seasonTpl)
        {
            _target = seasonTpl;
        }

        public string ToHtml() => _target.ToHtml();
        public string ToJson() => _target.ToJson();
    }
}
