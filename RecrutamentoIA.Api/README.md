# RecrutamentoIA.Api

Backend (API) do sistema de **ranqueamento de currículos com IA**: recebe a descrição de uma
vaga e arquivos de currículo (PDF, DOCX, DOC ou TXT), extrai o texto de cada arquivo e usa um
agente de IA (Anthropic Claude) para calcular um `score` de compatibilidade e ordenar os
candidatos em um ranking, com pontos fortes, pontos fracos e habilidades identificadas.

## Stack

- .NET 8 — ASP.NET Core (Minimal API, estilo `Program.cs`)
- **PdfPig** — extração de texto de PDF
- **DocumentFormat.OpenXml** — extração de texto de DOCX
- **Anthropic Claude** (REST API) como agente de IA
- **Swashbuckle (Swagger)** + **Scalar.AspNetCore** para documentação
- Autenticação por **usuário/senha em CSV + JWT (HS256)**

## Estrutura

```
RecrutamentoIA.Api/
├── Program.cs                        # bootstrap: configuração, CORS, segurança, endpoints e arranque
├── Auth/                            # autenticação por usuário/senha (CSV) + JWT
│   ├── AuthEndpoints.cs            # POST /api/auth/registrar e /api/auth/login
│   ├── UsuariosEndpoints.cs        # GET /api/usuarios (exige usuário aprovado)
│   ├── JwtSettings.cs              # configuração "Jwt" (Secret, validação fail-closed)
│   ├── PasswordHasher.cs           # hash PBKDF2-SHA256 + salt (a senha nunca em texto puro)
│   ├── TokenService.cs             # emissão de tokens JWT (HS256)
│   ├── UserRecord.cs               # modelo de usuário (campo 'allowed')
│   └── UserRepository.cs           # persistência em Data/users.csv (sem banco de dados)
├── Data/
│   ├── users.csv                   # usuários reais (NÃO versionado, ver .gitignore)
│   └── users.example.csv           # exemplo do formato
├── Filters/
│   └── MultipartDocumentationFilter.cs   # documenta campos multipart no Swagger
├── Models/
│   └── AnaliseModels.cs            # CurriculoTexto, AnaliseResultado, AnaliseResponse
├── Services/
│   ├── AgenteService.cs            # lógica do agente de IA (Claude) + modo MOCK
│   └── ExtracaoTextoService.cs     # extrai texto de PDF/DOCX/TXT
├── Properties/
│   └── launchSettings.json         # perfis de execução (porta 5105)
├── appsettings.json                # configuração pública (Claude, Jwt, Limites)
├── appsettings.local.json          # segredos locais (NÃO versionado)
└── RecrutamentoIA.Api.csproj       # projeto .NET
```

## Como executar

Pré-requisito: .NET 8 SDK.

```powershell
cd RecrutamentoIA.Api
dotnet run
```

- API: `http://localhost:5105`
- Scalar: `http://localhost:5105/scalar`
- OpenAPI: `http://localhost:5105/openapi/v1.json`

> Antes de iniciar, defina o segredo JWT em `appsettings.local.json` (ignorado pelo git),
> seção `Jwt:Secret` — mínimo 32 bytes; sem ele a API **não inicia** (fail-closed).

## Arquitetura / fluxo

1. `POST /api/auth/registrar` cria o usuário em `Data/users.csv` (só hash PBKDF2 + salt).
2. `POST /api/auth/login` valida credenciais e devolve um **JWT (Bearer)**.
3. `POST /api/analisar` (protegido por JWT) com `descricaoVaga` + arquivos `curriculos`:
   - `ExtracaoTextoService` extrai o texto de cada arquivo (PDF/DOCX/TXT).
   - `AgenteService` consulta Claude e devolve um JSON com `score`, pontos fortes, pontos fracos e habilidades.
   - O resultado é ordenado por `score` descendente em um único ranking (`AnaliseResponse`).

## Endpoints principais

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/auth/registrar` | Cria usuário (hash PBKDF2) |
| POST | `/api/auth/login` | Devolve JWT |
| GET | `/api/usuarios` | Lista usuários (exige JWT + usuário aprovado) |
| POST | `/api/analisar` | Analisa e ranqueia currículos (multipart, exige JWT) |

## Configuração

As opções são controladas em `appsettings.json` → seções `Claude`, `Jwt` e `Limites`
(máx. 100 currículos por análise, máx. 8 chamadas simultâneas à IA, 50 MB por upload).

Sem `Claude:ApiKey` configurada, o sistema usa um **modo MOCK** (score por palavras-chave).