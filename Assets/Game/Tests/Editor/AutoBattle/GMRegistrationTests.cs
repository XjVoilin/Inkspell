using System.Linq;
using NUnit.Framework;
#if JULYGF_DEBUG
using cfg;
using July.Diagnostics;
#endif

namespace Game.Tests
{
    public sealed class GMRegistrationTests
    {
#if JULYGF_DEBUG
        [Test]
        public void CommandsAreInRuntimeAssemblyAndRegisteredByDomain()
        {
            Assert.That(typeof(SpellGM).Assembly, Is.SameAs(typeof(SpellAssetSystem).Assembly));
            Assert.That(typeof(BattleGM).Assembly, Is.SameAs(typeof(SpellAssetSystem).Assembly));
            var registry = new GMRegistry();
            Assert.That(registry.Register(typeof(SpellGM)), Is.True);
            Assert.That(registry.Register(typeof(BattleGM)), Is.True);
            Assert.That(registry.Categories.Select(category => category.Category),
                Is.EqualTo(new[] {"法术", "战斗"}));
            Assert.That(registry.Categories[0].Commands.Count, Is.EqualTo(5));
            Assert.That(registry.Categories[1].Commands.Count, Is.EqualTo(1));
            var add = registry.Categories[0].Commands.Single(command => command.Method.Name == nameof(SpellGM.AddPair));
            Assert.That(add.Params[0].ParamType, Is.EqualTo(typeof(SpellType)));
        }
#else
        [Test]
        public void WithoutDebugDefine_GMCommandsAndMutationMethodsAreAbsent()
        {
            var assembly = typeof(SpellAssetSystem).Assembly;
            Assert.That(assembly.GetType("Game.SpellGM"), Is.Null);
            Assert.That(assembly.GetType("Game.BattleGM"), Is.Null);
            Assert.That(typeof(SpellAssetSystem).GetMethods(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Any(method => method.Name.StartsWith("Debug")), Is.False);
        }
#endif
    }
}
