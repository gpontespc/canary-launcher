# Cenários manuais para atualização de versão

## Objetivo
Validar o comportamento do `ClientUpdater.DetermineUpdatePlanAsync` quando somente o carimbo de data/hora da versão muda ou quando há alteração em `major.minor`.

## Pré-requisitos
- Ter dois arquivos `launcher_config.json` (ou endpoints) preparados com combinações distintas de `clientVersion` conforme descrito.
- Limpar o diretório do client entre cenários para evitar efeitos de cache.
- Garantir que o `client_version.txt` reflita a versão local de cada cenário (pode ser editado manualmente ou utilizando uma execução anterior do launcher).

## Cenários

1. **Alteração apenas no timestamp**
   - Versão local: `1.5.202401010930`
   - Versão remota: `1.5.202401021100`
   - Resultado esperado: `UpdateMode.Assets` quando `assetsUrl` estiver configurada e acessível.

2. **Alteração em major.minor**
   - Versão local: `1.5.202401010930`
   - Versão remota: `1.6.202401010930`
   - Resultado esperado: `UpdateMode.Full`.

3. **Versão local sem timestamp (formato legado)**
   - Versão local: `15020230701` (somente números).
   - Versão remota: `1.5.202401021100`
   - Resultado esperado: comparação por fallback normalizado, resultando em `UpdateMode.Full` se a parte numérica remota for maior.

4. **Timestamp divergente sem assinatura remota**
   - Forçar falha ao baixar a assinatura (por exemplo, apontar `assetsUrl` para um servidor que retorne erro 404).
   - Versão local: `1.5.202401010930`
   - Versão remota: `1.5.202401021100`
   - Resultado esperado: `UpdateMode.Assets` mesmo sem assinatura disponível.

5. **Assinatura diferente com timestamps iguais**
   - Versão local: `1.5.202401010930`, assinatura local `abc`.
   - Versão remota: `1.5.202401010930`, assinatura remota `xyz`.
   - Resultado esperado: `UpdateMode.Assets` para manter compatibilidade com o comportamento anterior.

Registre o modo retornado em cada cenário (por exemplo, adicionando logs temporários ou depurando a aplicação) para confirmar que as regras foram aplicadas corretamente.
