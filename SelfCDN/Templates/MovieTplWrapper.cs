using Shared.Model.Templates;

namespace SelfCDN.Templates
{
    public class MovieTplWrapper : ITemplate
    {
        private readonly MovieTpl _target;

        public MovieTplWrapper(MovieTpl movieTpl)
        {
            _target = movieTpl;
        }
        public string ToHtml() => _target.ToHtml();
        public string ToJson() => _target.ToJson();
    }
}
