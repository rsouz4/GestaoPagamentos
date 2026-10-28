# 🔐 Autenticação do WhatsApp Web via Sessão Persistente

## 📋 Como Funciona

A aplicação agora **salva a sessão de autenticação** do WhatsApp Web após o primeiro login. Isso significa:

- ✅ **Primeira vez:** Você escaneia o QR Code para autenticar
- ✅ **Próximas vezes:** A autenticação é carregada automaticamente
- ✅ **Sem dependências:** Não precisa do Chrome aberto ou flags especiais
- ✅ **Portável:** Funciona em qualquer máquina com a sessão salva

---

## 🚀 Fluxo de Funcionamento

### Primeira Execução

```
1. Você abre GestaoPagamentos.Wpf
2. A aplicação inicia o Playwright/Chromium
3. Navega para https://web.whatsapp.com
4. Mostra o QR Code para escanear
5. Você escaneia com seu telefone
6. Sessão salva automaticamente em:
   C:\Users\[Seu Usuário]\AppData\Roaming\GestaoPagamentos\WhatsAppSession\
7. Auto-envio de notificações inicia ✓
```

### Execuções Futuras

```
1. Você abre GestaoPagamentos.Wpf
2. A aplicação carrega a sessão salva
3. Navega para WhatsApp Web usando cookies/session anterior
4. Já autenticado, sem precisar do QR Code ✓
5. Auto-envio inicia imediatamente ✓
```

---

## 📁 Onde a Sessão é Salva

A sessão é armazenada em:
```
C:\Users\[Seu Usuário]\AppData\Roaming\GestaoPagamentos\WhatsAppSession\
```

Arquivo: `storage_state.json` (contém cookies, localStorage e sessionStorage)

---

## 🔄 Gerenciando a Sessão

### ✅ Manter a Sessão Salva
Simplesmente use a aplicação normalmente. A sessão permanece válida enquanto o WhatsApp Web reconhecer a sessão no servidor.

### 🔄 Renovar a Sessão
Se a sessão expirar ou tiver problemas:

1. **Abra o Windows Explorer** e vá para:
   ```
   C:\Users\[Seu Usuário]\AppData\Roaming\GestaoPagamentos\WhatsAppSession\
   ```

2. **Apague o arquivo** `storage_state.json`

3. **Execute a aplicação novamente**

4. **Escaneie o QR Code** para criar uma nova sessão

### 🗑️ Limpar Completamente

Se quiser remover toda a sessão salva:

1. Feche a aplicação GestaoPagamentos.Wpf
2. Delete a pasta:
   ```
   C:\Users\[Seu Usuário]\AppData\Roaming\GestaoPagamentos\
   ```
3. Execute a aplicação novamente (criará nova sessão)

---

## ⚠️ Dúvidas Frequentes

**P: Quanto tempo a sessão fica válida?**
R: Depende do WhatsApp Web. Geralmente, a sessão é válida por 30-90 dias. Se expirar, você verá o QR Code novamente na próxima execução.

**P: É seguro guardar a sessão?**
R: Sim! A sessão é salva localmente no seu computador em `AppData\Roaming`. É tão seguro quanto deixar o WhatsApp Web aberto no seu navegador.

**P: Posso compartilhar a sessão entre computadores?**
R: Tecnicamente sim, mas **não recomendado** por segurança. Cada máquina deve ter sua própria sessão.

**P: E se eu mudar de conta no WhatsApp?**
R: Delete o arquivo `storage_state.json` e escaneie o QR Code da nova conta.

**P: A sessão funciona em modo headless?**
R: Sim! Por isso configuramos `ModoHeadless: true` no `appsettings.json`. O navegador roda em background sem mostrar a interface.

---

## 🛠️ Troubleshooting

### Problema: "QR Code continua aparecendo"

**Solução:**
1. Verifique se o arquivo foi salvo em:
   ```
   C:\Users\[Seu Usuário]\AppData\Roaming\GestaoPagamentos\WhatsAppSession\storage_state.json
   ```
2. Se não existir, a sessão não foi salva. Tente novamente.
3. Verifique os logs da aplicação (output do Debug).

### Problema: "Sessão expirou"

**Solução:**
1. Delete o arquivo `storage_state.json`
2. Execute a aplicação novamente
3. Escaneie o QR Code

### Problema: "Arquivo de sessão corrompido"

**Solução:**
1. Delete a pasta completa:
   ```
   C:\Users\[Seu Usuário]\AppData\Roaming\GestaoPagamentos\
   ```
2. Execute a aplicação novamente

---

## 📝 Logs

Para verificar se a sessão está sendo carregada/salva, procure por estes logs:

```
[Information] Carregando sessão salva de: C:\Users\...\storage_state.json
[Information] Sessão salva com sucesso em: C:\Users\...\storage_state.json
```

Se não aparecer "Carregando sessão salva", significa que a sessão anterior não existe (é a primeira vez).

---

## ✅ Checklist para Primeira Configuração

- [ ] Abra GestaoPagamentos.Wpf
- [ ] Aguarde a aplicação abrir e navigate para WhatsApp Web
- [ ] Escaneie o QR Code com seu celular
- [ ] Aguarde alguns segundos para a sessão ser salva
- [ ] Veja o log: "Sessão salva com sucesso em: ..."
- [ ] Feche e abra a aplicação novamente
- [ ] Verifique se agora carrega sem QR Code: "Carregando sessão salva de: ..."
- [ ] Pronto! ✅

---

## 🚀 Próximas Etapas

1. Execute a aplicação
2. Escaneie o QR Code do WhatsApp Web (primeira vez apenas)
3. A sessão será salva automaticamente
4. Próximas execuções carregarão a sessão sem precisar autenticar novamente

