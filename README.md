# 🎯 Recrutamento IA — Backend (ASP.NET Core Web API)

Backend do sistema de **ranking de currículos com IA**: recebe a descrição da vaga e os
currículos (PDF, DOCX ou TXT), extrai o texto e usa um agente de IA (Anthropic Claude)
para gerar um ranking de compatibilidade com pontos fortes, pontos fracos e habilidades identificadas.

## 🏗️ Stack

- .NET 8 (ASP.NET Core — Minimal API)
- `PdfPig` — extração de texto de PDF
- `DocumentFormat.OpenXml` — extração de texto de DOCX
- Anthropic Claude (REST API) como agente de IA
- Swashbuckle (Swagger) + Scalar.AspNetCore para documentação

## 📁 Estrutura

```
RecrutamentoIA.Api/
├── Auth/                       # autenticação por usuário/senha (CSV) + JWT
│   ├── AuthEndpoints.cs                # /api/auth/registrar e /api/auth/login
│   ├── JwtSettings.cs                  # configuração "Jwt" (Secret, validação fail-closed)
│   ├── PasswordHasher.cs                # hash PBKDF2-SHA256 + salt (senha nunca em texto puro)
│   ├── TokenService.cs                  # emissão de tokens JWT (HS256)
│   ├── UserRecord.cs / UserRepository.cs# persistência de usuários em Data/users.csv
├── Data/
│   └── users.example.csv                # exemplo do formato (o arquivo real NÃO é versionado)
├── Filters/
│   └── MultipartDocumentationFilter.cs
├── Models/
│   └── AnaliseModels.cs
├── Services/
│   ├── AgenteService.cs                # lógica do agente de IA (Claude)
│   └── ExtracaoTextoService.cs         # extrai texto de PDF/DOCX/TXT
├── Properties/
│   └── launchSettings.json
├── Program.cs
└── appsettings.json
```

## ▶️ Como rodar

Pré-requisito: .NET 8 SDK.

```powershell
cd RecrutamentoIA.Api
dotnet run
```

- API em `http://localhost:5105`
- Swagger: `http://localhost:5105/swagger`
- Scalar: `http://localhost:5105/scalar`

> Antes de rodar, defina o segredo JWT em `RecrutamentoIA.Api/appsettings.local.json`
> (arquivo ignorado pelo git), na seção `Jwt:Secret` — **mínimo 32 bytes**.
> Exemplo: `"Jwt": { "Secret": "<64 caracteres hexadecimais aleatórios>" }`.

## 🔑 Autenticação por usuário/senha + JWT

Os usuários ficam em `RecrutamentoIA.Api/Data/users.csv` (sem banco de dados) e a senha é
armazenada apenas como **hash PBKDF2 + salt** — nunca em texto puro.

1. Crie um usuário:
   ```http
   POST /api/auth/registrar
   Content-Type: application/json

   { "username": "admin", "password": "SenhaForte@123" }
   ```
2. Faça login e guarde o JWT devolvido:
   ```http
   POST /api/auth/login
   Content-Type: application/json

   { "username": "admin", "password": "SenhaForte@123" }
   ```
3. Use o token nas rotas protegidas (ex.: `/api/analisar`):
   ```http
   POST /api/analisar
   Authorization: Bearer SEU_TOKEN_JWT
   ```

- O segredo de assinatura vem de `Jwt:Secret` em `appsettings.local.json` (mínimo 32 bytes);
  sem ele a API **não inicia** (segurança fail-closed).
- As credenciais reais (`Data/users.csv`) **não vão para o git** (veja `.gitignore`);
  o modelo versionado é `Data/users.example.csv`.
- A documentação (Swagger/Scalar) fica **livre só em desenvolvimento**.

## 🔌 Endpoint

`POST /api/analisar` (multipart/form-data)
- `descricaoVaga` (string, obrigatório)
- `curriculos` (arquivos, 1 a N — PDF, DOCX ou TXT)

**Exemplo de resposta**:

```json
{
  "descricaoVaga": "...",
  "totalCurriculos": 3,
  "ranking": [
    {
      "nomeArquivo": "joao.pdf",
      "nomeCandidato": "João Silva",
      "score": 87,
      "resumo": "Forte match com a vaga...",
      "pontosFortes": ["...", "..."],
      "pontosFracos": ["..."],
      "habilidadesIdentificadas": ["C#", "React", "SQL"]
    }
  ],
  "processadoEm": "2026-08-14T18:00:00Z"
}
```

## 🔑 Configurando a API Key do Claude (Anthropic)

1. Gere uma chave em https://console.anthropic.com/ e escolha o modelo em `appsettings.json`.
2. Edite `RecrutamentoIA.Api/appsettings.json`:

```json
"Claude": {
  "ApiKey": "SUA_CHAVE_AQUI",
  "Model": "claude-haiku-4-5-20251001"
}
```

> Sem chave configurada, o backend usa um **modo MOCK** (score baseado em palavras-chave) para você testar a UI.

Em produção, prefira variável de ambiente ou User Secrets:

```powershell
dotnet user-secrets set "Claude:ApiKey" "SUA_CHAVE"
```

## 🔒 Segurança

- **A API Key da IA nunca vai para o frontend** — todas as chamadas à IA são feitas aqui no backend C#.
- O `appsettings.local.json` (que pode conter a chave real) **não é versionado** (veja `.gitignore`).

## 🔄 Trocando o provedor de IA

Toda a lógica do agente está em `Services/AgenteService.cs`.
Para usar OpenAI, Azure OpenAI ou Ollama, basta criar outra implementação da interface do agente
e trocar no `Program.cs` (`AddHttpClient<IAgenteIAService, ...>`).

## 📝 Limites atuais (configuráveis em `appsettings.json` → `Limites`)

- Máx. currículos por requisição: `Limites:MaxCurriculosPorAnalise` (padrão 100)
- Máx. chamadas simultâneas à IA: `Limites:MaxAnalisesSimultaneas` (padrão 8)
- Máx. 50 MB por upload total
- Currículo truncado em 8.000 caracteres antes de enviar à IA (economia de tokens)