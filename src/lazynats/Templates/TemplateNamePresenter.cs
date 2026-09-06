using lazynats.Components;

namespace lazynats.Templates;

internal sealed class TemplateNamePresenter: IValuePresenter<Template>
{
    public string Format(Template value) => value.Name;
}
