using System.Threading;
using Cysharp.Threading.Tasks;
using July.Arch;

namespace Game
{
    public sealed class SpellSynthesisSystem : SystemBase
    {
        public async UniTask<bool> TrySynthesizeAsync(
            long firstSpellId,
            long secondSpellId,
            CancellationToken ct = default)
        {
            var procedure = new SpellSynthesisProcedure(firstSpellId, secondSpellId);
            await RunProcedure(procedure, ct);
            return procedure.Succeeded;
        }
    }
}
