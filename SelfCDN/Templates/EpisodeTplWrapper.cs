using Shared.Model.Templates;

namespace SelfCDN.Templates
{
    public class EpisodeTplWrapper : ITemplate
    {
        private readonly EpisodeTpl _target;

        public EpisodeTplWrapper(EpisodeTpl episodeTpl)
        {
            _target = episodeTpl;
        }

        public string ToHtml() => _target.ToHtml();
        public string ToJson() => _target.ToJson();
    }
}
