# Matriz de Compatibilidade e Interoperabilidade DXF

**DotNetCadEngine — Especificação Técnica de Importação e Exportação DXF**

Este documento detalha o suporte e a conformidade formal do **DotNetCadEngine** com a especificação Autodesk DXF para as versões **AutoCAD R12 (AC1009)** e **AutoCAD 2000 (AC1015)**.

---

## 1. Visão Geral de Versões Suportadas

| Versão DXF | Código de Cabeçalho | Status de Exportação | Status de Importação | Notas |
| :--- | :--- | :--- | :--- | :--- |
| **AutoCAD R12** | `AC1009` | Suportado (Modo legado R12) | Suportado | Polilinhas estruturadas com sub-entidades `POLYLINE`, `VERTEX` e `SEQEND`. |
| **AutoCAD 2000** | `AC1015` | **Padrão Oficial** | Suportado | Suporte a `LWPOLYLINE` (código `90`), TrueColor RGB 24-bit (`420`), e `DIMENSION`. |
| **AutoCAD 2004+** | `AC1018`, `AC1021`, etc. | — | Parcial / Retrocompatível | Lê seções `HEADER`, `TABLES` e entidades 2D compatíveis; ignora tabelas 3D/BIM avançadas. |

---

## 2. Matriz de Entidades e Atributos

| Seção / Entidade | Códigos de Grupo DXF | Suporte na Importação | Suporte na Exportação | Comportamento de Round-Trip |
| :--- | :--- | :--- | :--- | :--- |
| **HEADER** | `9 $ACADVER`, `9 $INSUNITS`, `9 $EXTMIN`, `9 $EXTMAX` | Lê versão e mapeia `$INSUNITS` (`4` mm, `5` cm, `6` m, `1` in, `2` ft) para o `DisplayUnit` do documento | Escreve `$ACADVER`, `$INSUNITS`, `$EXTMIN` e `$EXTMAX` | 100% de preservação de unidades e enquadramento. |
| **TABLES / LAYER** | `2` Nome, `70` Flags, `62` ACI, `420` TrueColor, `6` Linetype | Lê nome, visibilidade (`62 < 0`), bloqueio (`flag & 4`), cor ACI e TrueColor `420` | Grava nome, visibilidade, bloqueio, ACI e TrueColor `420` | Preservação total de atributos de camada. |
| **LINE** | `8` Layer, `10/20/30`, `11/21/31`, `62`, `420` | Lê pontos inicial/final em 2D. Se `Z != 0`, projeta em `Z = 0` com aviso estruturado | Escreve coordenadas com precisão `F6`, camada, ACI `62` e TrueColor `420` | 100% preservado em 2D. |
| **CIRCLE** | `8` Layer, `10/20/30`, `40` Raio, `62`, `420` | Lê centro e raio em 2D | Escreve centro e raio com precisão `F6` | 100% preservado em 2D. |
| **ARC** | `8` Layer, `10/20`, `40` Raio, `50` StartAngle, `51` EndAngle, `62`, `420` | Lê centro, raio e ângulos em graus | Escreve centro, raio e ângulos com precisão `F6` (sem perda de precisão angular) | 100% preservado em 2D. |
| **LWPOLYLINE** | `8`, `70` Flags, `90` Vértices, `10/20` Coordenadas, `42` Bulge | Lê todos os vértices e flag de fechamento (`70 & 1`). Bulge != 0 gera aviso estruturado | Escreve polilinha leve com contagem de vértices e flag fechada | 100% preservada para segmentos retos. |
| **POLYLINE (R12)** | `0 POLYLINE`, `0 VERTEX`, `0 SEQEND` | Processa sequência completa de sub-registros `VERTEX` até `SEQEND` | No modo R12, gera `POLYLINE` + `VERTEX` + `SEQEND`. No modo R2000 gera `LWPOLYLINE` | Retrocompatibilidade 100% com desenhos AutoCAD R12. |
| **TEXT** | `8`, `10/20/30`, `40` Altura, `50` Rotação, `1` Texto, `62`, `420` | Lê posição, altura, ângulo em graus e texto UTF-8 | Escreve texto com altura e rotação `F6` | 100% preservado. |
| **MTEXT** | `8`, `10/20`, `40`, `50`, `1`, `3` | Remove códigos de formatação de parágrafo (`\P` -> `\n`) e formatações inline, importando como `CadText` | Exporta como `TEXT` padrão | Texto recuperado com aviso no relatório. |
| **DIMENSION** | `8`, `10/20`, `13/23`, `14/24`, `1` Override, `70` Tipo | Lê pontos de definição (`13/23` início, `14/24` fim, `10/20` offset) e texto customizado `1`, reconstruindo `CadDimension` | Exporta entidade nativa `0\nDIMENSION` preservando os pontos de cota | **Semântica de cota preservada (não é explodida em linhas soltas)**. |

---

## 3. Mapeamento de Cores: ACI e TrueColor 24-bit

O DXF suporta dois sistemas de cores:
1. **ACI (AutoCAD Color Index):** Paleta clássica indexada de 1 a 255.
   - `1` = Vermelho (`#EF4444`)
   - `2` = Amarelo (`#EAB308`)
   - `3` = Verde (`#10B981`)
   - `4` = Ciano (`#06B6D4`)
   - `5` = Azul (`#3B82F6`)
   - `6` = Magenta (`#D946EF`)
   - `7` = Branco / Preto (`#FFFFFF`)
   - `8` = Cinza Claro (`#94A3B8`)
   - `9` = Cinza Médio (`#CBD5E1`)
2. **TrueColor (Código de Grupo 420):** Inteiro 24-bit representando `(R << 16) | (G << 8) | B`.
   - O DotNetCadEngine grava **ambos** os códigos `62` (ACI mais próximo para compatibilidade retroativa) e `420` (valor hexadecimal exato `#RRGGBB`).
   - Na importação, a presença do código `420` tem prioridade sobre o `62`, garantindo fidelidade cromática absoluta no round-trip.

---

## 4. Relatório Estruturado de Importação (`DxfImportResult`)

A importação produz uma instância de `DxfImportResult`:
- `DetectedAcadVersion`: versão identificada no cabeçalho (ex: `"AC1015"`).
- `ImportedEntityCount`: número de entidades convertidas com sucesso.
- `SkippedEntityCount`: número de entidades desconhecidas ou não tratadas (ex: `HATCH`, `SPLINE`, `3DFACE`).
- `Warnings`: lista de advertências estruturadas contendo código de erro, tipo da entidade, índice do par e mensagem detalhada.
