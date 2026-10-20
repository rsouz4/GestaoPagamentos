# Gestão de Pagamentos — Guia do cliente

Este guia explica como utilizar os recursos disponíveis na versão atual da aplicação para Windows e como são calculadas as dívidas, parcelas e pagamentos.

> **Atenção às taxas atuais:** o sistema acrescenta **30% de juros na criação da dívida** e aplica **15% de multa por dia de atraso sobre o valor original da parcela, sem capitalização**. São regras diferentes, descritas abaixo. Esses percentuais não são editáveis nas telas atuais. Confirme o alinhamento dessas regras com o responsável pelo sistema antes de utilizar a aplicação em operações reais. Este documento descreve o software; não substitui o contrato firmado com o devedor nem uma avaliação jurídica das cobranças.

## 1. Visão geral

O menu lateral oferece:

| Tela | Para que serve |
| --- | --- |
| **Dashboard** | Consultar valores a receber, valores recebidos, atrasos e quantidade de dívidas ativas. |
| **Devedores** | Cadastrar e consultar as pessoas vinculadas às dívidas. |
| **Dívidas** | Criar dívidas avulsas ou parceladas, consultar parcelas e registrar pagamentos. |
| **Notificações** | Gerar lembretes e avisos de atraso e abrir conversas no WhatsApp. |
| **Configurações** | Ajustar a antecedência dos lembretes, os modelos de mensagem e o intervalo de abertura das conversas em lote. |

**Fluxo recomendado:** cadastrar o devedor → criar a dívida → conferir as parcelas → registrar os pagamentos recebidos → acompanhar os saldos no Dashboard.

O aplicativo controla registros financeiros. **Registrar um pagamento não faz uma transferência, não cobra o devedor e não confirma recebimento bancário.** Registre somente valores cujo recebimento você já conferiu.

## 2. Cadastrar um devedor

1. Abra **Devedores**.
2. Clique em **Novo** para limpar o formulário e iniciar um cadastro.
3. Preencha **Nome**, que é obrigatório.
4. Informe, se desejar, **Telefone (WhatsApp)**, **E-mail** e **Observação**.
5. Clique em **Salvar**.

Para usar as notificações, informe um telefone válido com DDD. Exemplo de formato: `(11) 91234-5678`.

Para localizar alguém, preencha o campo de busca e clique em **Buscar**. Para editar, selecione a pessoa na lista, altere os campos e clique em **Salvar**.

**Regra de exclusão:** o sistema bloqueia a exclusão de devedores com dívidas ativas. Evite excluir cadastros apenas para corrigir nome ou telefone; utilize a edição.

## 3. Criar uma dívida

1. Abra **Dívidas**.
2. Clique em **Nova dívida**.
3. Preencha os campos:

| Campo | Como preencher |
| --- | --- |
| **Devedor \*** | Selecione uma pessoa já cadastrada. |
| **Descrição \*** | Identifique a operação, por exemplo: “Empréstimo — acordo de março”. Não pode ficar em branco. |
| **Valor emprestado (R$) \*** | Digite o valor originalmente emprestado, maior que zero. Exemplo: `1.000,00`. Não acrescente os 30% manualmente. |
| **Tipo** | Escolha **Avulsa** para uma única parcela ou **Parcelada** para dividir o total. |
| **Quantidade de parcelas** | Exibida para dívidas parceladas. Informe um número inteiro de pelo menos 1. |
| **Primeiro vencimento** | Escolha o vencimento da primeira parcela. O formulário sugere a data de hoje mais 30 dias; confira antes de salvar. |
| **Observação** | Campo opcional para informações sobre o acordo. |

4. Após preencher o valor e sair do campo, confira a prévia do **valor a receber**, já com os 30% de juros.
5. Clique em **Salvar**.
6. A aplicação abrirá os detalhes da dívida. Confira os valores e os vencimentos gerados.

O botão **Cancelar** do formulário sai sem salvar essa nova dívida. Ele não é a mesma ação que **Cancelar dívida**, disponível nos detalhes de uma dívida já cadastrada.

### Exemplo: dívida avulsa

- Valor emprestado: **R$ 1.000,00**.
- Juros iniciais de 30%: **R$ 300,00**.
- Valor a receber, antes de eventual atraso: **R$ 1.300,00**.
- Tipo **Avulsa**: uma parcela de **R$ 1.300,00**, na data escolhida.

### Exemplo: dívida parcelada

Para **R$ 100,00** emprestados em **3 parcelas**:

- Total com os juros iniciais: **R$ 130,00**.
- Parcelas: **R$ 43,33**, **R$ 43,33** e **R$ 43,34**.
- A diferença de centavos fica na última parcela, preservando o total da dívida.

Os vencimentos são **mensais**, calculados a partir da primeira data escolhida, e não a cada 30 dias. Por exemplo, se a primeira parcela vencer em **31/01/2026**, as seguintes vencerão em **28/02/2026** e **31/03/2026**. Quando o mês não tem o dia escolhido, utiliza-se o último dia daquele mês. Não há ajuste automático para fins de semana ou feriados.

A **data do empréstimo** exibida na listagem é registrada como a data do computador no momento da criação. Não há campo nessa tela para informar uma data de empréstimo diferente. Na listagem de dívidas, **Data vencimento** corresponde ao vencimento da última parcela; consulte os detalhes para ver todos os vencimentos.

## 4. Regras de negócio e cálculos

### 4.1. Juros iniciais

O valor total inicial é calculado assim:

**Valor a receber = valor emprestado × 1,30.**

Esse acréscimo ocorre **uma vez na criação**, tanto em dívidas avulsas quanto parceladas. Não são 30% ao mês nem 30% adicionais em cada parcela. O total já acrescido é que será dividido entre as parcelas.

Os valores monetários são calculados em centavos. O total inicial é arredondado para duas casas decimais; a distribuição das parcelas mantém a soma igual a esse total, ajustando os centavos na última parcela.

### 4.2. Multa por atraso

- No dia do vencimento, não há multa de atraso.
- A partir do dia seguinte, a parcela não quitada passa a estar atrasada.
- São considerados dias corridos, incluindo fins de semana e feriados.
- A cada dia de atraso, o sistema acrescenta **15% sobre o valor original da parcela**, que já inclui os juros iniciais de 30%. O acréscimo diário é fixo: **não incide sobre multas acumuladas**.
- O cálculo é **simples, sem capitalização**. Não se trata de uma multa cobrada uma única vez: o mesmo valor é acrescentado a cada dia enquanto houver saldo a quitar.
- **Pagamentos parciais não reduzem a base da multa diária.** O acréscimo continua sendo calculado sobre o valor original da parcela até a quitação completa.
- O acréscimo diário é arredondado para centavos.
- Em um dia com pagamento, o sistema calcula primeiro a multa daquele dia e depois abate o pagamento.
- Cada parcela tem seu próprio vencimento e cálculo de atraso.

**Exemplo sem pagamentos**, para uma parcela de R$ 1.300,00:

| Momento | Acréscimo do dia | Saldo da parcela |
| --- | --- | --- |
| Dia do vencimento | R$ 0,00 | R$ 1.300,00 |
| 1º dia de atraso | R$ 195,00 | R$ 1.495,00 |
| 2º dia de atraso | R$ 195,00 | R$ 1.690,00 |
| 3º dia de atraso | R$ 195,00 | R$ 1.885,00 |

Sem pagamentos, a multa acumulada é **valor diário arredondado × dias de atraso**. Nesse exemplo, são R$ 195,00 por dia; no segundo dia a multa acumulada é R$ 390,00, não há multa sobre multa.

O saldo apresentado nas consultas é calculado para a data atual. Não é necessário cadastrar a multa manualmente. Mantenha a data do computador correta e atualize ou reabra a tela para consultar os valores novamente.

### 4.3. Como o pagamento abate a dívida

Os pagamentos são registrados **por parcela**. O sistema utiliza o valor recebido nesta ordem:

1. Abater a multa acumulada da parcela.
2. Abater o valor previsto ainda não pago da parcela, que já inclui os juros iniciais de 30%.

Por exemplo: no primeiro dia de atraso do caso acima, há R$ 195,00 de multa. Um pagamento de **R$ 195,00** nesse dia quita somente essa multa; o saldo ainda será **R$ 1.300,00**. Se não houver quitação, o saldo continua sujeito aos acréscimos dos dias seguintes.

Se o pagamento nesse primeiro dia for **R$ 695,00**, R$ 195,00 abatem a multa e R$ 500,00 abatem o valor da parcela, restando **R$ 800,00**. No segundo dia, acrescentam-se os mesmos **R$ 195,00**, pois a base continua sendo a parcela original de R$ 1.300,00; o saldo passa a **R$ 995,00**. Após a quitação completa, não há novos acréscimos.

São aceitos pagamentos parciais, mas o valor deve ser **maior que zero** e não pode ultrapassar o saldo da parcela calculado para a **data informada no pagamento**. O pagamento não é distribuído automaticamente entre outras parcelas.

### 4.4. Status das parcelas e das dívidas

| Status da parcela | Significado |
| --- | --- |
| **Pendente** | Ainda não venceu e não possui pagamento. Inclui o próprio dia do vencimento. |
| **Paga parcial** | Possui pagamento, ainda tem saldo e não está vencida. |
| **Atrasada** | Passou do vencimento e ainda tem saldo, mesmo que já tenha recebido pagamento parcial. |
| **Paga** | O pagamento cobriu o valor previsto e a multa devida, se houver. |

| Status da dívida | Significado |
| --- | --- |
| **Ativa** | Há parcela ainda não quitada. Uma dívida com atraso continua com status **Ativa**; o atraso é identificado nas parcelas. |
| **Quitada** | Todas as parcelas foram quitadas. |
| **Cancelada** | Foi cancelada pelo usuário; não aceita novos pagamentos nem exclusão de pagamentos. |

## 5. Consultar uma dívida e registrar pagamentos

1. Abra **Dívidas**.
2. Se necessário, use o filtro **Ativas**, **Quitadas**, **Canceladas** ou **Todas**. A tela inicia mostrando as ativas.
3. Selecione a dívida e clique em **Abrir**.
4. Na lista de **Parcelas**, selecione a parcela que recebeu o pagamento.
5. No painel **Registrar pagamento**, confira o valor, informe a **Data** real do recebimento e, se desejar, uma observação.
6. Clique em **Registrar**.
7. Confira o histórico e os novos valores da parcela.

Ao selecionar uma parcela com saldo, o campo de pagamento é preenchido com o restante consultado para hoje. **Confira esse valor antes de registrar**, principalmente se o recebimento foi parcial ou ocorreu em outra data. Alterar a data do pagamento não recalcula automaticamente a sugestão desse campo.

### Entendendo os valores nos detalhes

- **Valor emprestado:** valor original informado na criação.
- **Valor a receber:** total inicial com os 30% de juros; não inclui multas posteriores.
- **Pago:** soma dos pagamentos registrados.
- **Restante:** soma dos saldos das parcelas para hoje, incluindo as multas de atraso aplicáveis.

Por isso, em caso de atraso, **Restante** pode ser maior que a simples diferença entre **Valor a receber** e **Pago**.

### Corrigir um pagamento lançado incorretamente

1. Abra a dívida e selecione a parcela.
2. Em **Pagamentos da parcela**, selecione o lançamento incorreto.
3. Clique em **Excluir pagamento** e confirme.
4. Se necessário, registre novamente com os dados corretos.

A exclusão recalcula os saldos considerando que aquele pagamento não existe mais. Pode aumentar o saldo de atraso e reabrir uma dívida antes quitada. Ela **não realiza estorno bancário**. Dívidas canceladas não permitem essa alteração.

## 6. Cancelar uma dívida

Nos detalhes, clique em **Cancelar dívida** e confirme a ação.

O cancelamento:

- Marca a dívida como **Cancelada**, sem apagar seus registros.
- Bloqueia novos pagamentos e a exclusão dos pagamentos existentes.
- Descarta as notificações pendentes vinculadas às parcelas da dívida.
- Retira a dívida dos totais do Dashboard, **inclusive os pagamentos que ela já recebeu**.

Não utilize o cancelamento para indicar recebimento: a quitação acontece ao pagar todas as parcelas. Também não confunda cancelamento com devolução de dinheiro. **Não há ação de reativar dívida cancelada nas telas atuais.**

## 7. Acompanhar pelo Dashboard

- **A receber:** saldo atualizado das parcelas abertas das dívidas ativas, incluindo multas aplicáveis.
- **Recebido:** pagamentos registrados nas dívidas não canceladas, inclusive nas já quitadas. Não representa apenas o lucro ou apenas os juros.
- **Em atraso:** parte do saldo a receber referente às parcelas vencidas e não quitadas. **Já está incluída em A receber**; não some os dois indicadores.
- **Dívidas ativas:** quantidade de dívidas com esse status.
- **Parcelas em atraso:** lista com o devedor, a dívida, o vencimento, os dias de atraso e o saldo atualizado.

Use **Atualizar** para consultar novamente os indicadores depois de alterações.

## 8. Lembretes e cobranças pelo WhatsApp

1. Confira o telefone do devedor em **Devedores**.
2. Abra **Notificações** e clique em **Gerar notificações**.
3. Selecione um lembrete ou aviso de atraso.
4. Confira a **Mensagem** e edite o texto se necessário.
5. Clique em **Enviar no WhatsApp**.
6. Na conversa aberta, confira o destinatário e **confirme o envio no próprio WhatsApp**.

### Regras das notificações

- A geração considera parcelas não quitadas de dívidas ativas.
- **Lembrete:** para parcelas com vencimento entre hoje e o limite de antecedência configurado. O padrão é **3 dias**.
- **Atraso:** para parcelas cujo vencimento já passou.
- Ao gerar um aviso de atraso, lembretes ainda pendentes daquela parcela são descartados.
- O sistema evita criar outra notificação do mesmo tipo para a mesma parcela, mesmo se a anterior já tiver sido enviada, ignorada ou descartada. Não há cobrança diária repetida automática.
- **Ignorar** retira a notificação da fila pendente; não altera nem quita a dívida.
- A quitação da parcela descarta as notificações pendentes dela.

**Importante:** o aplicativo abre a conversa com o texto preparado e registra a notificação como enviada ao abrir o link. Isso **não comprova que você clicou em Enviar, que a mensagem foi entregue ou que foi lida**. É necessário ter acesso ao WhatsApp para concluir o envio.

A ação de envio em lote dos atrasados abre as conversas uma por vez e pula telefones inválidos. Ainda é necessário confirmar cada envio no WhatsApp. **Cancelar lote** interrompe as próximas aberturas; não desfaz as que já ocorreram.

As mensagens guardam o texto preparado na geração. Se o saldo mudou depois por atraso ou pagamento, confira e atualize a mensagem antes do envio individual. O lote utiliza as mensagens já armazenadas.

## 9. Configurações disponíveis

Em **Configurações**, é possível alterar:

| Configuração | Padrão e funcionamento |
| --- | --- |
| **Dias de antecedência do lembrete** | 3 dias. Aceita zero ou mais. |
| **Intervalo entre envios em lote (ms)** | 1.500 ms, equivalentes a 1,5 segundo entre aberturas. Aceita zero ou mais. |
| **Modelo de lembrete** | Texto utilizado para novos lembretes. Não pode ficar vazio. |
| **Modelo de atraso** | Texto utilizado para novos avisos de atraso. Não pode ficar vazio. |

Os modelos aceitam os marcadores `{Nome}`, `{Valor}`, `{Descricao}` e `{Vencimento}`, substituídos pelos dados da parcela na geração. Clique em **Salvar** após as alterações. Alterar o modelo não reescreve mensagens já geradas.

**Essa tela não altera os juros iniciais nem a multa diária.**

## 10. Dados e cuidados de uso

Na configuração padrão, os dados ficam em um banco local no Windows, em **`%LOCALAPPDATA%\GestaoPagamentos\gestao_pagamentos.db`**. O local pode ser diferente se a instalação tiver sido configurada pelo suporte.

- Os dados não são sincronizados automaticamente entre computadores ou usuários do Windows.
- Para uma cópia de segurança manual, feche completamente a aplicação e copie a pasta de dados para um local seguro. Solicite apoio do suporte antes de restaurar ou transferir o banco.
- Não apague o banco para tentar corrigir um lançamento.
- Preserve os dados pessoais dos devedores e restrinja o acesso ao computador e às cópias de segurança.
- Confira sempre devedor, parcela, valor e data antes de salvar uma operação.

## 11. Limites da versão atual

- Não há campo nas telas para negociar taxas individuais, conceder descontos ou alterar valores e vencimentos das parcelas depois da criação.
- Não há ação nas telas para reativar uma dívida cancelada.
- O controle é local; o registro de pagamentos não possui confirmação automática do banco.
- O WhatsApp depende da confirmação manual do usuário; não há confirmação de entrega ou leitura pela aplicação.
- Não há rotina de backup automático disponível nas telas.

Em caso de erro de cadastro ou divergência de saldo que não possa ser resolvida pelos passos deste guia, informe ao suporte a dívida, a parcela, as datas e os valores envolvidos antes de fazer novos lançamentos.
