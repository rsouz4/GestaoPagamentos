# Automação de Envio WhatsApp Web via Playwright

## 📋 Visão Geral

Este documento descreve a implementação de automação de envio de mensagens via **WhatsApp Web** utilizando **Playwright** em modo headless no WPF GestaoPagamentos.

### O que é
Integração de automação de navegador (Playwright/Chromium) para enviar mensagens individuais via WhatsApp Web sem intervenção manual além da autenticação inicial.

### Como Funciona
1. Playwright abre navegador Chromium em modo headless (invisível)
2. Acessa WhatsApp Web (https://web.whatsapp.com)
3. Aguarda autenticação (primeira execução exige QR Code manual)
4. Para cada mensagem:
   - Navega até o link de contato (wa.me/telefone)
   - Localiza caixa de mensagem
   - Digita o texto com delays realistas
   - Envia mediante Enter ou botão

---

## ⚠️ Riscos e Limitações Críticas

### 1. **Risco de Bloqueio/Banimento pelo WhatsApp**

#### Nível de Risco
- **Baixo** para 1-5 mensagens/dia
- **Médio** para 5-20 mensagens/dia
- **Alto** para mais de 20 mensagens/dia (sem intervalos)

#### Por que WhatsApp bloqueia
- Detecção de automação (headers HTTP, user-agent, padrão de requisições)
- Velocidade de envio anormal (sem delays entre ações)
- Múltiplas conexões simultâneas
- Violação dos Termos de Serviço

#### Como minimizar risco
✅ **Implementado neste projeto:**
- Delays realistas entre ações (800ms-1500ms)
- User-Agent legítimo (Chrome real)
- Args Playwright anti-detecção: `--disable-blink-features=AutomationControlled`
- Modo headless com recursos desabilitados
- Envio individual, não em massa
- Intervalo de 2 segundos entre mensagens em lotes

✅ **Recomendações adicionais:**
- **Não enviar mais de 5-10 mensagens/dia** por número
- **Variar horários** de envio (não em horários exatos, tipo 10:00)
- **Incluir delay aleatório** entre envios (500-3000ms)
- **Monitorar logs** de erro e parar se houver falhas
- **Revisar Termos de Serviço** do WhatsApp regularmente

⚠️ **Aviso Legal:** O WhatsApp pode banir sua conta sem aviso prévio se detectar automação. Use por sua conta e risco.

---

### 2. **WhatsApp Pode Exigir Autenticação Manual (QR Code)**

#### Situações que exigem autenticação
- Primeira execução do navegador
- Mudança de IP/localização
- Expiração da sessão (7-30 dias típico)
- WhatsApp detectando atividade suspeita
- Logout forçado pelo WhatsApp

#### Como funciona neste projeto
1. `VerificarAutenticacaoAsync()` aguarda elemento `div[data-testid='chat-list']`
2. Se não encontrar em **120 segundos**, assume que QR Code é necessário
3. **Modo headless desativado** automaticamente para exibir QR
4. Usuário escaneia QR manualmente no navegador
5. Sessão é reutilizada em próximas execuções

#### Limitação
- Sem intervenção humana, automação **falha se QR Code é solicitado**
- Playwright headless **não pode exibir QR visualmente** em modo puro
- Solução: Alternar para modo visual (`Headless: false`) em ConfigApp

---

### 3. **Mudanças Frequentes no WhatsApp Web**

#### Por que acontece
- WhatsApp atualiza interface regularmente
- Seletores CSS mudam (`div[contenteditable]`, `button[aria-label]`)
- Novo layout pode quebrar automação completamente

#### Sintomas de quebra
```
PlaywrightException: "element not found"
PlaywrightException: "Timeout waiting for selector"
```

#### Solução
- Monitorar logs de erro: `ILogger<PlaywrightWhatsAppService>`
- Atualizar seletores CSS em `PlaywrightWhatsAppService`
- Testar manualmente com navegador visual periodicamente
- Considerar alternativas se quebra for frequente

---

### 4. **Problemas de Connectivity e Timeout**

#### Cenários
- Perda de internet durante envio
- Lentidão da rede (WhatsApp Web lento)
- Servidores WhatsApp indisponíveis
- Firewall/VPN bloqueando WhatsApp

#### Timeouts configuráveis (appsettings.json)
```json
{
  "WhatsAppAutomation": {
	"TimeoutCarregamentoPagina": "00:00:30",    // Esperar página carregar
	"TimeoutAutenticacao": "00:02:00",          // Esperar QR/autenticação
	"TimeoutCampoDeMensagem": "00:00:15"        // Esperar caixa de texto
  }
}
```

#### Melhorar confiabilidade
- Aumentar timeouts se rede for lenta
- Implementar retry automático (não está implementado)
- Testar em ambiente com internet estável

---

### 5. **Compatibilidade e Dependências**

#### Navegadores suportados
- ✅ **Chromium** (instalado por padrão via Playwright)
- ❌ Firefox, Safari não suportados (apenas Chromium testado)

#### Plataforma
- ✅ Windows 10/11
- ⚠️ Linux/macOS (requer testes)
- ❌ ARM64 (não testado)

#### .NET
- ✅ .NET 10 (conforme projeto)
- ⚠️ Versões anteriores podem ter issues com async/await

#### Espaço em disco
- Chromium headless shell: ~150 MB
- Playwright binários: ~500 MB total
- Dados de sessão: ~50-200 MB

---

### 6. **Limitações de Funcionalidade**

#### Não suportado
- ❌ Enviando mídia (imagens, vídeos, áudio)
- ❌ Grupos (apenas chats diretos)
- ❌ Verificação de entrega/leitura
- ❌ Histórico de mensagens
- ❌ Emojis complexos (algumas limitações)

#### Suportado
- ✅ Texto puro
- ✅ Quebras de linha (`\n`)
- ✅ Links
- ✅ Caracteres especiais (acentos, pontuação)
- ✅ Agendamento (controlado pela aplicação, não pelo serviço)

---

## 🔒 Boas Práticas de Segurança

### 1. **Dados Sensíveis**
- 🚫 Não armazene senhas de WhatsApp no código
- ✅ Sessão é salva automaticamente pelo Chromium
- ⚠️ Limpe histórico de sessão em ambientes compartilhados

### 2. **Logs**
- Logs incluem telefones e mensagens (PII - informação pessoal identificável)
- Configure retenção de logs adequada
- Não compartilhe logs contendo telefones/mensagens

### 3. **Acesso à Aplicação**
- Restrinja acesso ao WPF para usuários autorizados
- Usuário pode enviar mensagens para qualquer devedor
- Implemente auditoria se necessário

---

## 📊 Monitoramento e Troubleshooting

### Logs importantes
```csharp
// Em appsettings.json ou código
"Logging": {
  "LogLevel": {
	"GestaoPagamentos.Infrastructure.Notificacoes": "Information"
  }
}
```

### Verificação de funcionamento
1. Abra DevTools (`F12`) em navegador visual para debugar seletores
2. Monitore `bin/Debug/net10.0-windows/Chromium` para erros
3. Verifique conexão internet antes de enviar lote
4. Teste com uma mensagem primeiro antes de lote

### Recuperação de falhas
- **Autenticação expirou**: Abra WPF, acesse WhatsApp Web, re-autentique
- **Timeout na mensagem**: Aumentar `TimeoutCampoDeMensagem` em appsettings
- **Bloqueio WhatsApp**: Aguarde 24-48h e tente novamente (ou entre em contato com WhatsApp)

---

## 🚀 Alternativas e Futuras Melhorias

### Alternativas consideradas
1. **API Oficial WhatsApp Business**
   - ✅ Seguro, estável
   - ❌ Caro, requer aprovação, delay de resposta
   - 💰 Preço: ~$0.01-0.05 por mensagem

2. **Serviços Terceiros (Twilio, MessageBird)**
   - ✅ Confiável, documentado
   - ❌ Mensagens aparecem de número diferente
   - 💰 Preço: $0.01-0.1 por mensagem

3. **Automação via Selenium (alternativa Playwright)**
   - ✅ Mais maduro, maior comunidade
   - ❌ Mais lento, mais pesado
   - ⚠️ Ambos têm risco de bloqueio

### Melhorias futuras para este projeto
- [ ] Retry automático com backoff exponencial
- [ ] Pool de contextos para reutilização de sessões
- [ ] Suporte a múltiplos números WhatsApp
- [ ] Agendamento de envios
- [ ] Webhook de confirmação (quando WhatsApp liberar)
- [ ] Dashboard de histórico de automações
- [ ] Alertas de bloqueio/erro crítico

---

## 📖 Referências e Recursos

### Documentação
- [Playwright .NET Docs](https://playwright.dev/dotnet/)
- [WhatsApp Web (não oficial)](https://github.com/pedroslopez/whatsapp-web.js)
- [Termos de Serviço WhatsApp](https://www.whatsapp.com/legal/)

### Comunidade
- GitHub Issues: Playwright
- Stack Overflow: `playwright` + `whatsapp`
- Fóruns: r/whatsapp, r/webautomation

---

## ✅ Checklist de Implantação

Antes de usar em produção:

- [ ] Testou com 1 mensagem com sucesso
- [ ] Revisou Termos de Serviço WhatsApp
- [ ] Configurou timeouts apropriados para sua rede
- [ ] Definiu política de máximo de mensagens/dia (recomendação: 3-5)
- [ ] Configurou logging para auditoria
- [ ] Realizou teste com lote pequeno (3-5 mensagens)
- [ ] Documentou processo de recuperação em caso de bloqueio
- [ ] Treinou usuários sobre limitações
- [ ] Testou em ambiente similar à produção

---

## 📞 Suporte e Contato

Para problemas:
1. Verifique logs em `GestaoPagamentos.Infrastructure.Notificacoes`
2. Teste com navegador visual (mudar `ModoHeadless: false`)
3. Verifique conexão internet
4. Abra issue no GitHub com logs sanitizados

**Última atualização**: 2026-10-24

**Status**: ⚠️ Experimental - Use com cautela em produção
