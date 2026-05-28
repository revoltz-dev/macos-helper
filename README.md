# macOS Helper

Um aplicativo para Windows que baixa instaladores oficiais do macOS direto da Apple e cria pendrives bootáveis para instalar/reinstalar macOS em um Mac.

Tudo em uma única janela, em português, sem precisar de Terminal, scripts ou tutoriais de Hackintosh.

---

## O que ele faz

- **Detecta seu modelo de Mac** a partir do `Model Identifier` (Sobre Este Mac → Informações do Sistema) e mostra qual é a última versão de macOS compatível com ele.
- **Baixa o instalador oficial da Apple** — escolhendo de um catálogo completo desde o OS X Lion até o macOS Tahoe (versão mais recente).
- **Filtra automaticamente** as versões compatíveis com o seu Mac, então você não baixa algo que nem vai instalar.
- **Cria o pendrive bootável** com um clique. O pendrive funciona direto no Mac: segure ⌥ Option ao ligar e selecione o instalador.

---

## Para quem é

- Quem está com um **Mac sem sistema**, com disco apagado ou com macOS quebrado, e precisa reinstalar.
- Quem só tem um **PC com Windows** à mão e não consegue criar o pendrive pelo método tradicional do Mac.
- Quem quer **voltar para uma versão antiga** do macOS (downgrade) usando um pendrive feito do zero.
- Quem quer **testar betas** (público, customer seed ou developer beta).

---

## Como usar

1. **Abra** o `MacOSHelper.exe`. Ele pede permissão de administrador — necessário porque grava direto no pendrive.
2. Clique em **Detectar Mac**, cole a saída do comando do `system_profiler` (ou só o `Model Identifier`) e veja qual macOS é compatível.
3. Clique em **Catálogo** → **Carregar Catálogo**. Escolha entre Release Público, Beta, Customer Seed ou Developer Beta.
4. Escolha a versão de macOS que quer e clique em **Baixar**. O download é resumível — se cair, é só retomar.
5. Conecte o pendrive (mínimo **16 GB**), volte para a tela principal.
6. Selecione o pendrive no combo **Pendrive**, escolha o **Instalador** baixado e clique em **Criar Pendrive Bootável**.
7. Confirme. Em alguns minutos o pendrive está pronto. **Tudo no pendrive será apagado.**

---

## No Mac

1. Ligue o Mac segurando a tecla **⌥ Option (Alt)**.
2. Selecione o ícone do **instalador macOS** no menu de boot.
3. Use o **Utilitário de Disco** para apagar e formatar como APFS (ou HFS+ em Macs antigos).
4. Volte e escolha **Instalar macOS**.

### Se aparecer "O servidor de recuperação não pôde ser contatado"

Esse erro é causado por um bug da Apple no High Sierra (10.13) — eles atualizaram os certificados dos servidores mas o framework de segurança do Recovery não consegue mais validar HTTPS. **Solução simples:** abra **Utilitários → Terminal** e cole este comando (uma linha só):

```
nvram IASUCatalogURL="http://swscan.apple.com/content/catalogs/others/index-10.13-10.12-10.11-10.10-10.9-mountainlion-lion-snowleopard-leopard.merged-1.sucatalog"
```

O truque é trocar `https://` por `http://` (sem o `S`), o que pula a verificação SSL quebrada. Feche o Terminal e clique em **Reinstalar macOS** novamente.

Para outras versões do macOS o caminho é o mesmo — só ajuste o `10.13` da URL para a versão que estiver instalando (ex: `10.14` para Mojave).

---

## Requisitos

- **Windows 10 ou 11** (64 bits)
- **Permissão de administrador** (é solicitada automaticamente)
- **Pendrive de 16 GB ou mais** (todos os dados serão apagados)
- Conexão à internet para baixar o instalador (4–14 GB dependendo da versão)

---

## Download

Pegue a versão mais recente em **[Releases](../../releases)** — é um único `.exe`, não precisa instalar nada.

---

## Observações

- O app **não roda no Mac** — ele é exatamente para quem só tem Windows à mão.
- Suporte completo para gravação de pendrive funciona até o **macOS Catalina (10.15)**. Versões Big Sur+ (11+) podem ser baixadas, mas a gravação no pendrive ainda não é suportada nessa versão. Para Macs Apple Silicon, o boot via pendrive não é suportado pela própria Apple — use o método de Internet Recovery do próprio Mac.
- Os instaladores ficam salvos numa pasta `Downloads/` ao lado do `.exe`, então você pode reaproveitar sem baixar de novo.
