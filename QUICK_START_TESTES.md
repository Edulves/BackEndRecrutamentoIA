# TESTE RÁPIDO - Começar em 5 Minutos

---

## ⚡ PASSO 1: Iniciar Servidor (Terminal 1)

```bash
cd RecrutamentoIA.Api
dotnet run
```

Aguarde até ver:
```
Now listening on: http://localhost:5105
```

---

## ⚡ PASSO 2: Executar Script de Testes (Terminal 2)

```powershell
cd backend
.\test-api.ps1
```

**O que vai acontecer:**
1. ✅ Registra usuário `testuser`
2. ✅ Faz login (gera JWT)
3. ✅ Lista candidatos
4. ✅ Analisa 2 currículos (teste-a.txt + teste-b.txt)
5. ✅ Mostra ranking com scores

**Output esperado:**
```
✅ Usuário registrado com sucesso
✅ Login realizado com sucesso
✅ Candidatos listados
Total: 7
✅ Análise concluída
Tempo: 1200 ms

📊 Ranking de Candidatos:

  Arquivo: teste-a.txt
  Candidato: João Silva
  Score: 55/100
  Habilidades: C#, SQL, Azure, Git
```

---

## 🌐 PASSO 3: Interface Gráfica (Opcional)

Acesse no browser:
```
http://localhost:5105/scalar
```

Interface interativa para testar todos os endpoints.

---

## ⚙️ SOLUÇÃO: Usuário Não Aprovado

Se receber erro:
```
"Sua conta ainda não foi aprovada para uso"
```

**Solução:**
1. Abra `RecrutamentoIA.Api/Data/users.csv`
2. Procure pela linha do `testuser`
3. Mude de:
   ```
   testuser,hash_aqui,false
   ```
   Para:
   ```
   testuser,hash_aqui,true
   ```
4. Salve e re-execute o script

---

## 📊 TESTES ADICIONAIS

### Teste com Postman

1. **POST** `http://localhost:5105/api/auth/login`
   - Body (JSON): `{"username":"testuser","password":"Senha@123!"}`
   - Copie o `token` da response

2. **POST** `http://localhost:5105/api/analisar`
   - Header: `Authorization: Bearer {seu_token}`
   - Body (form-data):
     - `descricaoVaga`: "Desenvolvedor Full Stack"
     - `curriculos`: Selecione `RecrutamentoIA.Api/teste-a.txt`

---

## 🎯 ENDPOINTS PRINCIPAIS

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| POST | `/api/auth/registrar` | Registrar usuário |
| POST | `/api/auth/login` | Fazer login (JWT) |
| GET | `/api/candidatos` | Listar candidatos |
| POST | `/api/analisar` | ⭐ Analisar currículos |
| GET | `/api/vagas` | Listar vagas |
| POST | `/api/vagas` | Cadastrar vaga |

---

## 📝 NOTAS

- **Porta:** 5105
- **JWT Validade:** 1 hora
- **Upload Máx:** 50 MB
- **Currículos Suportados:** PDF, DOCX, TXT
- **Documentação:** http://localhost:5105/scalar

---

## ❌ ERROS COMUNS

| Erro | Solução |
|------|---------|
| 401 Unauthorized | Token inválido ou expirado |
| 403 Forbidden | Usuário não aprovado (users.csv) |
| 400 Bad Request | Falta arquivo em 'curriculos' |
| Connection refused | Servidor não está rodando (dotnet run) |

---

## ✅ CHECKLIST COMPLETO

- [ ] Servidor rodando em 5105
- [ ] Script test-api.ps1 executado com sucesso
- [ ] 2+ currículos analisados
- [ ] Ranking exibido com scores
- [ ] Habilidades identificadas corretamente
- [ ] Pontos fortes/fracos listados
- [ ] Tempo de resposta < 2 seg
- [ ] Acesso interface Scalar em 5105/scalar

---

**Tudo pronto para usar! 🚀**
