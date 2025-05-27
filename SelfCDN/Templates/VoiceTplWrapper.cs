using Shared.Model.Templates;

namespace SelfCDN.Templates
{
    public class VoiceTplWrapper : ITemplate
    {
        private readonly VoiceTpl _target;

        public VoiceTplWrapper(VoiceTpl episodeTpl)
        {
            _target = episodeTpl;
        }

        public string ToHtml() => _target.ToHtml();
        public string ToJson() => _target.ToJson();
    }
}