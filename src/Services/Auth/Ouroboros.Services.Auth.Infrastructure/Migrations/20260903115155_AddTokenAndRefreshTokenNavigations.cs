using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ouroboros.Services.Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenAndRefreshTokenNavigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration intencionalmente vazia. Token e RefreshToken ganharam referências navegáveis
            // (Token.TokenType, Token.User, RefreshToken.User) mapeadas para as mesmas colunas e chaves
            // estrangeiras que já existiam desde a InitialCreate: muda o modelo do EF Core, não o banco.
            // Ela existe só para manter o snapshot do modelo em dia com o código.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
