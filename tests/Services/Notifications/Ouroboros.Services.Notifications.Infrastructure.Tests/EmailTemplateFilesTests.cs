using Ouroboros.Contracts.Notifications;
using Ouroboros.Services.Notifications.Application;

namespace Ouroboros.Services.Notifications.Infrastructure.Tests;

public class EmailTemplateFilesTests
{
	[Theory]
	[InlineData(EmailTemplateKeys.AuthEmailConfirmation)]
	[InlineData(EmailTemplateKeys.AuthPasswordReset)]
	public void EveryCatalogedTemplate_HasItsFileShippedWithTheBuild(string templateKey)
	{
		var template = EmailTemplateCatalog.Find(
			key: templateKey,
			version: 1,
			locale: "pt-BR"
		);

		Assert.NotNull(template);

		// O catálogo e os arquivos precisam andar juntos: uma chave sem arquivo só falharia na hora de
		// entregar, com o usuário já esperando o e-mail.
		var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", template.FileName);
		Assert.True(File.Exists(templatePath), $"Template não encontrado: {templatePath}");
	}

	[Fact]
	public void Find_WithUnknownVersion_ReturnsNothing()
	{
		var template = EmailTemplateCatalog.Find(
			key: EmailTemplateKeys.AuthEmailConfirmation,
			version: 99,
			locale: "pt-BR"
		);

		Assert.Null(template);
	}
}
