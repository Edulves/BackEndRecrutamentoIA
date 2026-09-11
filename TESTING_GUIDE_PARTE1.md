# Guia Completo de Testes - API de Ranking de Currículos

**Porta:** http://localhost:5105  
**Documentação Interativa:** http://localhost:5105/scalar

---

## 🚀 INICIANDO O SERVIDOR

```bash
cd RecrutamentoIA.Api
dotnet run
```

Você verá:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5105
```

---

## 📋 FLUXO DE TESTES COMPLETO

### **PASSO 1: Acessar Documentação Interativa**
```
http://localhost:5105/scalar
```

---

### **PASSO 2: Registrar um Usuário**

**Endpoint:** `POST /api/auth/registrar`

**Request:**
```json
{
  "username": "testuser",
  "password": "Senha@123!"
}
```

**cURL:**
```bash
curl -X POST http://localhost:5105/api/auth/registrar \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"Senha@123!"}'
```

---

### **PASSO 3: Aprovar Usuário**

Edite `RecrutamentoIA.Api/Data/users.csv`:
```
testuser,HASH_AQUI,true
```
(mude `false` para `true` na terceira coluna)

---

### **PASSO 4: Fazer Login (Obter JWT)**

**Endpoint:** `POST /api/auth/login`

**Request:**
```json
{
  "username": "testuser",
  "password": "Senha@123!"
}
```

**cURL:**
```bash
curl -X POST http://localhost:5105/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"Senha@123!"}'
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

**Copie o token!**

---

### **PASSO 5: Listar Candidatos (Teste de Autenticação)**

**Endpoint:** `GET /api/candidatos`

**cURL:**
```bash
TOKEN="seu_token_aqui"
curl -X GET http://localhost:5105/api/candidatos \
  -H "Authorization: Bearer $TOKEN"
```

---

### **PASSO 6: ANÁLISE DE CURRÍCULOS (Endpoint Principal) ⭐**

Este é o teste mais importante!

**Endpoint:** `POST /api/analisar`

**cURL:**
```bash
TOKEN="seu_token_aqui"

curl -X POST http://localhost:5105/api/analisar \
  -H "Authorization: Bearer $TOKEN" \
  -F "descricaoVaga=Desenvolvedor Full Stack. C#, .NET, SQL, Azure, React, Python, Docker, AWS." \
  -F "curriculos=@RecrutamentoIA.Api/teste-a.txt" \
  -F "curriculos=@RecrutamentoIA.Api/teste-b.txt"
```

**Response (200 OK):**
```json
{
  "descricaoVaga": "Desenvolvedor Full Stack...",
  "totalCurriculos": 2,
  "ranking": [
    {
      "nomeArquivo": "teste-a.txt",
      "nomeCandidato": "João Silva",
      "score": 55,
      "resumo": "Candidato com experiência em C#, .NET, SQL...",
      "pontosFortes": [
        "Domínio em C# e .NET",
        "SQL Server"
      ],
      "pontosFracos": [
        "Sem React",
        "Sem Python"
      ],
      "habilidadesIdentificadas": ["C#", "SQL", "Azure"]
    }
  ],
  "processadoEm": "2026-09-08T10:30:45.123Z"
}
```

---

## 📁 ARQUIVOS DE TESTE PRONTOS

Use os arquivos já no projeto:

**teste-a.txt:**
```
Curriculo de exemplo A - Candidato João Silva.
Experiência com C#, .NET, SQL Server e Azure.
Skills: c#, sql, azure, git
```

**teste-b.txt:**
```
Curriculo de exemplo B - Candidata Maria Souza.
Experiência com Python, Docker, AWS e React.
Skills: python, docker, aws, react, node
```

---

## 🧪 TESTE COM VS CODE (REST Client)

Instale extensão "REST Client" no VS Code.

Crie arquivo `test.http`:
```http
### Login
POST http://localhost:5105/api/auth/login
Content-Type: application/json

{
  "username": "testuser",
  "password": "Senha@123!"
}

### Analisar 2 currículos
POST http://localhost:5105/api/analisar
Authorization: Bearer seu_token_aqui
Content-Type: multipart/form-data; boundary=----Boundary

------Boundary
Content-Disposition: form-data; name="descricaoVaga"

Desenvolvedor Full Stack
------Boundary
Content-Disposition: form-data; name="curriculos"; filename="teste-a.txt"
Content-Type: text/plain

< ./RecrutamentoIA.Api/teste-a.txt
------Boundary--
```

Clique "Send Request" acima de cada bloco.

---

## 🔍 TESTES DE OTIMIZAÇÃO (Medir Performance)

### **Teste: Verificar Cache em Ação**

Primeira requisição (sem cache):
```bash
time curl -X POST http://localhost:5105/api/analisar \
  -H "Authorization: Bearer $TOKEN" \
  -F "descricaoVaga=Dev Full Stack" \
  -F "curriculos=@teste-a.txt"
```

Segunda requisição (com cache):
```bash
time curl -X POST http://localhost:5105/api/analisar \
  -H "Authorization: Bearer $TOKEN" \
  -F "descricaoVaga=Dev Full Stack" \
  -F "curriculos=@teste-a.txt"
```

**Esperado:**
- Primeira: ~1-2 seg
- Segunda: ~0.5 seg (cache reutilizado)

---

## ⚠️ PROBLEMAS COMUNS

### **Erro 401 Unauthorized**
- Verificar se token está no header
- Token pode ter expirado (validade: 1 hora)

### **Erro 403 Forbidden**
- Usuário não foi aprovado (users.csv: mude false para true)

### **Erro 400 Bad Request**
- Falta arquivo em 'curriculos'

### **Erro 413 Payload Too Large**
- Limite é 50 MB, reduza tamanho dos arquivos

