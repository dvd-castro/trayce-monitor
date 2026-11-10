# Marca Trayce Monitor

Kit da marca do Trayce Monitor (nome público desde 10/11/2026; "Trayce" é a forma curta e o símbolo não muda) (conceito **Traço**: o T com a barra feita da linha de 60 s do painel). Fonte do desenho: Card 100, Entrega 016 (Diego, design). Os arquivos foram gerados por script a partir de uma única geometria; não edite à mão, peça a regeração.

## O que usar onde

| Onde | Arquivo |
|---|---|
| Ícone do `.exe`, atalho, Iniciar, Alt+Tab, instalador | `trayce.ico` (16, 20, 24, 28, 32, 40, 48, 64 e 256 px, cada tamanho desenhado na sua grade) |
| PNG do ícone (lojas, site, `ApplicationIcon` alternativo) | `png/trayce-icone-app-<px>.png` (16 a 512) |
| README e documentação, fundo claro | `svg/trayce-horizontal.svg` |
| README e documentação, fundo escuro | `svg/trayce-horizontal-fundo-escuro.svg` |
| Uma cor | `svg/*-mono-fundo-claro.svg` (tinta #1A1A1A) e `svg/*-mono-fundo-escuro.svg` (branco) |
| Símbolo sozinho | `svg/trayce-simbolo*.svg`; a 16 px use `svg/trayce-simbolo-16px.svg` |
| Empilhado (telas quadradas, sobre) | `svg/trayce-empilhado*.svg` |
| Logotipo com o nome completo, horizontal (fundo claro / escuro / uma cor) | `svg/trayce-monitor-horizontal.svg`, `-fundo-escuro`, `-mono-fundo-claro`, `-mono-fundo-escuro` (PNG: `png/trayce-monitor-horizontal*-90px.png`) |
| Logotipo com o nome completo, empilhado | `svg/trayce-monitor-empilhado*.svg` (PNG: `png/trayce-monitor-empilhado*-435px.png`) |
| Cores no app C# | `tokens.json` |

No README do GitHub, troque claro e escuro com `<picture>`:

```html
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/brand/svg/trayce-monitor-horizontal-fundo-escuro.svg">
  <img alt="Trayce Monitor" src="assets/brand/svg/trayce-monitor-horizontal.svg" width="420">
</picture>
```

## Regras curtas

- O ícone do app usa a **placa azul** (#005FB8 com símbolo branco): o `.ico` não troca com o tema e a placa funciona na barra clara e na escura.
- O anel da bandeja continua sendo o **estado** (spec v2.2); a marca não vai para a bandeja.
- Área de proteção: metade da altura do T em volta do horizontal; ¼ do lado em volta do símbolo.
- Tamanho mínimo: horizontal "Trayce" com 80 px de largura e horizontal "Trayce Monitor" com 200 px; abaixo disso, só o símbolo (mínimo 16 px).
- Não distorcer, girar, trocar as cores, pôr sombra ou contorno, nem reescrever "Trayce Monitor" com outra fonte. Em "Trayce Monitor", "Monitor" é Manrope Regular (400) no mesmo tamanho do "Trayce" (SemiBold) na horizontal e menor (altura de maiúscula pela metade, espaçado) no empilhado; só o T tem a onda.
- Os controles do app seguem a cor de destaque do Windows; o azul Trayce aparece só onde a marca aparece (ícone, instalador, Sobre, README).

## Tipografia e licença

O nome "Trayce" está desenhado em **Manrope SemiBold** (The Manrope Project Authors, SIL Open Font License 1.1) e **convertido em path**: nenhum arquivo daqui embarca a fonte. A interface do app usa Segoe UI Variable, do sistema.

## Nome

O nome público é **Trayce Monitor** (decisão do Dono, 10/11/2026): já existe outro software chamado Trayce (ferramenta de desenvolvedor para tráfego de contêineres Docker, trayce.dev). O kit anterior (`trayce-*.svg`) continua válido para o símbolo e o ícone; para o nome por extenso use os arquivos `trayce-monitor-*`. Repositório: https://github.com/dvd-castro/trayce-monitor
