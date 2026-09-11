# 🌐 Como Acessar Documentação Interativa (Scalar/Swagger)

**Versão de Produção - Porta 5000**

---

## 📋 Passos Rápidos

### **1️⃣ Parar Servidor Atual**
Pressione `Ctrl+C` no terminal onde o servidor está rodando

### **2️⃣ Recompilar com Novo Código**
```bash
cd RecrutamentoIA.Api
dotnet build -c Release
```

### **3️⃣ Executar Servidor Novamente**
```bash
cd RecrutamentoIA.Api
dotnet run -c Release
```

Aguarde: `Now listening on: http://localhost:5000`

### **4️⃣ Abrir Documentação no Browser**

**Opção A - Scalar (Recomendado):**
```
http://localhost:5000/scalar
```

**Opção B - Swagger UI:**
```
http://localhost:5000/swagger/index.html
```

**Opção C - OpenAPI JSON:**
```
http://localhost:5000/openapi/v1.json
```

---

## 🎯 Como Testar Manualmente no Scalar/Swagger

### **Passo 1: Registrar Usuário**

1. Clique em `POST /api/auth/registrar`
2. Clique em "Try it out"
3. Preencha o body:
```json
{
  "username": "testuser",
  "password": "Senha@123!"
}
```
4. Clique "Execute"
5. Veja a resposta: `"Usuário registrado com sucesso"`

---

### **Passo 2: Aprovar Usuário**

1. Abra arquivo: `RecrutamentoIA.Api/Data/users.csv`
2. Procure pela linha com `testuser`
3. Mude de:
   ```
   testuser,HASH_AQUI,false
   ```
   Para:
   ```
   testuser,HASH_AQUI,true
   ```
4. Salve o arquivo

---

### **Passo 3: Fazer Login**

1. No Scalar, clique em `POST /api/auth/login`
2. Clique em "Try it out"
3. Preencha o body:
```json
{
  "username": "testuser",
  "password": "Senha@123!"
}
```
4. Clique "Execute"
5. **Copie o `token` da resposta** (vai precisar dele!)

---

### **Passo 4: Usar Token nos Outros Endpoints**

1. No Scalar, clique em qualquer endpoint protegido (ex: `GET /api/candidatos`)
2. Clique em "Try it out"
3. Procure pelo campo "Authorization" (ou clique no cadeado 🔒 no topo)
4. Preencha:
   ```
   Bearer {SEU_TOKEN_AQUI}
   ```
   Exemplo:
   ```
   Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
   ```
5. Clique "Execute"

---

### **Passo 5: Testar Análise de Currículos** ⭐

Este é o endpoint principal!

1. Clique em `POST /api/analisar`
2. Clique em "Try it out"
3. Adicione o token no header Authorization (Bearer {TOKEN})
4. Preencha os campos:
   - **descricaoVaga:** "Desenvolvedor Full Stack. Requisitos: C#, .NET, SQL, Azure, React, Python, Docker, AWS."
   - **curriculos:** Selecione os arquivos:
     - `RecrutamentoIA.Api/teste-a.txt`
     - `RecrutamentoIA.Api/teste-b.txt`
5. Clique "Execute"
6. Veja o ranking com scores, pontos fortes, pontos fracos e habilidades!

---

## 🔑 Token JWT

Após fazer login, você recebe algo como:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0dXNlciIsImV4cCI6MTY5NDI5NzI4OCwiaWF0IjoxNjk0MjkzNjg4fQ.1234567890",
  "expiresIn": 3600
}
```

**Use este token em todos os endpoints protegidos:**
- Header: `Authorization: Bearer {token}`
- Válido por 1 hora (3600 segundos)

---

## 📊 Endpoints Disponíveis

### Autenticação (Públicos - Sem Token)
- `POST /api/auth/registrar` - Registrar novo usuário
- `POST /api/auth/login` - Fazer login

### Análise (Protegidos - Precisa de Token)
- `POST /api/analisar` - ⭐ **Analisar currículos contra vaga**
- `GET /api/candidatos` - Listar candidatos
- `DELETE /api/candidatos/{id}` - Excluir candidato
- `GET /api/candidatos/{id}/curriculo` - Download de PDF
- `GET /api/candidatos/{id}/foto` - Download de foto
- `POST /api/candidatos/{id}/foto` - Upload de foto

### Vagas (Protegidos - Precisa de Token)
- `GET /api/vagas` - Listar vagas
- `POST /api/vagas` - Cadastrar vaga (analisa todos os candidatos)

### Usuários (Protegidos - Precisa de Token)
- `GET /api/usuarios` - Listar usuários
- `POST /api/usuarios/{username}/permissoes` - Aprovar/bloquear usuário

---

## ✅ Checklist

- [ ] Servidor parado (Ctrl+C)
- [ ] Recompilado (`dotnet build -c Release`)
- [ ] Servidor rodando novamente (`dotnet run -c Release`)
- [ ] Acessou `http://localhost:5000/scalar` no browser
- [ ] Registrou usuário `testuser`
- [ ] Aprovou usuário em `users.csv`
- [ ] Fez login e copiu o token
- [ ] Testou `/api/candidatos` com token
- [ ] Testou `/api/analisar` com 2 currículos
- [ ] Viu o ranking com scores

---

## 🐛 Problemas Comuns

| Problema | Solução |
|----------|---------|
| "Scalar não carrega" | Servidor pode estar em modo Development. Certifique-se de rodar `dotnet run -c Release` |
| "401 Unauthorized" | Você não enviou o token no header Authorization |
| "403 Forbidden" | Usuário não foi aprovado em `users.csv` |
| "Arquivo não encontrado" | Use o caminho completo: `RecrutamentoIA.Api/teste-a.txt` |

---

## 🎯 Resumo

**URL Principal:** `http://localhost:5000/scalar`

**Sequência de Testes:**
1. Registrar → Login → Copiar Token
2. Listar Candidatos → Analisar Currículos
3. Ver Ranking com Scores

**Tempo estimado:** 5 minutos

---

**Tudo pronto! Acesse a documentação e comece a testar! 🚀**
