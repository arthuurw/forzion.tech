using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GrupoMuscularEnum = forzion.tech.Domain.Enums.TipoGrupoMuscular;
using TierPlanoEnum = forzion.tech.Domain.Enums.TierPlano;

namespace forzion.tech.Infrastructure.Seed;

public class DataSeeder(
    AppDbContext context,
    IPasswordHasher passwordHasher,
    IConfiguration configuration,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<DataSeeder> logger)
{
    private static readonly string[] GruposMuscularesPadrao =
    [
        "Peito", "Costas", "Ombro", "Biceps", "Triceps", "Pernas", "Gluteos", "Core", "FullBody"
    ];

    private static readonly (GrupoMuscularEnum Grupo, string Nome, string? Descricao)[] ExerciciosGlobais =
    [
        (GrupoMuscularEnum.Peito, "Supino Reto com Barra", "Empurre a barra partindo do peito até a extensão total dos braços."),
        (GrupoMuscularEnum.Peito, "Supino Inclinado com Halteres", "Movimento de empurre em banco inclinado a 30-45°."),
        (GrupoMuscularEnum.Peito, "Supino Declinado com Barra", "Supino em banco declinado, ênfase na porção inferior do peitoral."),
        (GrupoMuscularEnum.Peito, "Supino Reto com Halteres", "Versão com halteres permite maior amplitude de movimento."),
        (GrupoMuscularEnum.Peito, "Crucifixo com Halteres", "Abertura controlada dos braços para alongar e contrair o peitoral."),
        (GrupoMuscularEnum.Peito, "Crossover no Cabo", "Adução horizontal com cabos, ideal para finalizar o treino de peito."),
        (GrupoMuscularEnum.Peito, "Peck Deck", "Máquina de adução horizontal com foco no peitoral."),
        (GrupoMuscularEnum.Peito, "Flexão de Braços", "Exercício calistênico clássico para peitoral, tríceps e ombros."),
        (GrupoMuscularEnum.Peito, "Pullover com Halter", "Alongamento e contração do peitoral em amplitude total."),
        (GrupoMuscularEnum.Peito, "Crossover Baixo no Cabo", "Cabo preso na parte inferior, enfatiza a porção superior do peitoral."),

        (GrupoMuscularEnum.Costas, "Puxada Frontal no Pulley", "Puxe a barra até a altura do queixo com pegada aberta."),
        (GrupoMuscularEnum.Costas, "Remada Curvada com Barra", "Tronco inclinado, puxe a barra em direção ao abdômen."),
        (GrupoMuscularEnum.Costas, "Remada Unilateral com Halter", "Apoiado no banco, puxe o halter até a linha do quadril."),
        (GrupoMuscularEnum.Costas, "Levantamento Terra", "Exercício composto de alta intensidade para toda a cadeia posterior."),
        (GrupoMuscularEnum.Costas, "Puxada Fechada no Pulley", "Pegada neutra fechada, ênfase na porção inferior do latíssimo."),
        (GrupoMuscularEnum.Costas, "Remada Cavalinho", "Remada com a barra apoiada no chão em um extremo."),
        (GrupoMuscularEnum.Costas, "Barra Fixa (Pronada)", "Puxe o corpo até o queixo ultrapassar a barra com pegada pronada."),
        (GrupoMuscularEnum.Costas, "Remada Baixa no Cabo", "Sentado, puxe o triângulo até o abdômen com cotovelos juntos ao corpo."),
        (GrupoMuscularEnum.Costas, "Pulldown com Braços Retos", "Em pé, deprima as escápulas e pressione a barra de cima para baixo."),
        (GrupoMuscularEnum.Costas, "Hiperextensão Lombar", "Extensão do tronco no banco romano para fortalecer a lombar."),

        (GrupoMuscularEnum.Ombro, "Desenvolvimento com Barra", "Pressione a barra do nível dos ombros até a extensão total."),
        (GrupoMuscularEnum.Ombro, "Desenvolvimento com Halteres", "Versão com halteres permite maior amplitude e equilíbrio bilateral."),
        (GrupoMuscularEnum.Ombro, "Elevação Lateral com Halteres", "Eleve os braços lateralmente até a linha dos ombros."),
        (GrupoMuscularEnum.Ombro, "Elevação Frontal com Halteres", "Eleve os braços à frente até a linha dos ombros."),
        (GrupoMuscularEnum.Ombro, "Remada Alta com Barra", "Puxe a barra verticalmente até o queixo, cotovelos acima dos punhos."),
        (GrupoMuscularEnum.Ombro, "Face Pull no Cabo", "Puxe a corda em direção ao rosto, ideal para o deltoide posterior."),
        (GrupoMuscularEnum.Ombro, "Arnold Press", "Rotação dos halteres durante o movimento de desenvolvimento."),
        (GrupoMuscularEnum.Ombro, "Rotação Externa com Cabo", "Exercício de rotator cuff com cabo baixo."),
        (GrupoMuscularEnum.Ombro, "Elevação Lateral com Cabo", "Versão com cabo mantém tensão constante no deltoide lateral."),
        (GrupoMuscularEnum.Ombro, "Desenvolvimento Militar", "Desenvolvimento com barra livre em pé, exige maior estabilização do core."),

        (GrupoMuscularEnum.Biceps, "Rosca Direta com Barra", "Flexão de cotovelo bilateral com barra reta ou W."),
        (GrupoMuscularEnum.Biceps, "Rosca Alternada com Halteres", "Flexão de cotovelo alternada com supinação do antebraço."),
        (GrupoMuscularEnum.Biceps, "Rosca Concentrada", "Cotovelo apoiado na coxa, máxima contração do bíceps."),
        (GrupoMuscularEnum.Biceps, "Rosca Martelo", "Pegada neutra, trabalha braquial e braquiorradial."),
        (GrupoMuscularEnum.Biceps, "Rosca Scott", "Banco Scott isola o bíceps ao eliminar a compensação do ombro."),
        (GrupoMuscularEnum.Biceps, "Rosca no Cabo", "Cabo mantém tensão constante durante toda a amplitude."),
        (GrupoMuscularEnum.Biceps, "Rosca Inclinada com Halteres", "Banco inclinado aumenta o alongamento do bíceps."),
        (GrupoMuscularEnum.Biceps, "Rosca Inversa com Barra", "Pegada pronada trabalha braquiorradial e bíceps."),
        (GrupoMuscularEnum.Biceps, "Rosca Zottman", "Sobe supinado e desce pronado, trabalha toda a musculatura do braço."),
        (GrupoMuscularEnum.Biceps, "Rosca 21", "Dividida em 3 fases de 7 repetições para máxima fadiga."),

        (GrupoMuscularEnum.Triceps, "Tríceps Testa com Barra", "Flexione e estenda os cotovelos levando a barra até a testa."),
        (GrupoMuscularEnum.Triceps, "Tríceps Pulley Corda", "Extensão de cotovelo com corda no cabo, abertura na fase final."),
        (GrupoMuscularEnum.Triceps, "Tríceps Pulley Barra", "Extensão de cotovelo com barra reta ou V no cabo."),
        (GrupoMuscularEnum.Triceps, "Mergulho entre Bancos", "Apoio nas mãos em dois bancos, flexione e estenda os cotovelos."),
        (GrupoMuscularEnum.Triceps, "Tríceps Francês com Halter", "Extensão overhead unilateral com halter."),
        (GrupoMuscularEnum.Triceps, "Tríceps Kickback com Halter", "Extensão de cotovelo com tronco inclinado."),
        (GrupoMuscularEnum.Triceps, "Supino Fechado com Barra", "Pegada fechada no supino, ênfase nos tríceps."),
        (GrupoMuscularEnum.Triceps, "Mergulho em Paralelas", "Imersão em paralelas com corpo levemente inclinado à frente."),
        (GrupoMuscularEnum.Triceps, "Extensão Overhead no Cabo", "Cabo preso atrás do corpo, extensão de cotovelo acima da cabeça."),
        (GrupoMuscularEnum.Triceps, "Tríceps Unilateral no Cabo", "Extensão unilateral de cotovelo para correção de desequilíbrios."),

        (GrupoMuscularEnum.Pernas, "Agachamento Livre", "Exercício composto fundamental para quadríceps, glúteos e isquiotibiais."),
        (GrupoMuscularEnum.Pernas, "Leg Press 45°", "Prensa inclinada, permite alto volume com menor estresse na lombar."),
        (GrupoMuscularEnum.Pernas, "Extensão de Joelhos", "Isolamento do quadríceps na cadeira extensora."),
        (GrupoMuscularEnum.Pernas, "Flexão de Joelhos", "Isolamento dos isquiotibiais na mesa flexora."),
        (GrupoMuscularEnum.Pernas, "Afundo com Halteres", "Passo largo à frente com halteres, trabalha unilateralmente."),
        (GrupoMuscularEnum.Pernas, "Agachamento Hack", "Barra atrás das pernas, ênfase no quadríceps."),
        (GrupoMuscularEnum.Pernas, "Stiff com Halteres", "Joelhos semiflexionados, ênfase nos isquiotibiais e glúteos."),
        (GrupoMuscularEnum.Pernas, "Cadeira Abdutora", "Abertura das pernas para trabalhar abdutores do quadril."),
        (GrupoMuscularEnum.Pernas, "Cadeira Adutora", "Fechamento das pernas para trabalhar adutores do quadril."),
        (GrupoMuscularEnum.Pernas, "Panturrilha em Pé", "Elevação do calcanhar em pé para gastrocnêmio e sóleo."),

        (GrupoMuscularEnum.Gluteos, "Hip Thrust com Barra", "Extensão de quadril com barra sobre o colo, máxima ativação glútea."),
        (GrupoMuscularEnum.Gluteos, "Agachamento Sumô", "Postura larga com pés apontados para fora, ênfase nos glúteos."),
        (GrupoMuscularEnum.Gluteos, "Elevação Pélvica no Chão", "Versão bodyweight do hip thrust, ideal para iniciantes."),
        (GrupoMuscularEnum.Gluteos, "Afundo Reverso", "Passo para trás, maior ênfase nos glúteos versus afundo frontal."),
        (GrupoMuscularEnum.Gluteos, "Agachamento Búlgaro", "Uma perna apoiada atrás, alta ativação de glúteo e quadríceps."),
        (GrupoMuscularEnum.Gluteos, "Glúteo na Polia Baixa", "Extensão de quadril com cabo preso ao tornozelo."),
        (GrupoMuscularEnum.Gluteos, "Abdução de Quadril com Cabo", "Afastamento lateral da perna com cabo preso ao tornozelo."),
        (GrupoMuscularEnum.Gluteos, "Step Up com Halteres", "Subida ao banco com halteres, trabalho unilateral."),
        (GrupoMuscularEnum.Gluteos, "Stiff Unilateral com Halter", "Equilíbrio em uma perna com maior amplitude e ativação glútea."),
        (GrupoMuscularEnum.Gluteos, "Coice com Caneleira", "Extensão de quadril em quatro apoios com caneleira."),

        (GrupoMuscularEnum.Core, "Prancha Isométrica", "Contração isométrica de todo o core em posição de apoio."),
        (GrupoMuscularEnum.Core, "Abdominal Crunch", "Flexão do tronco com amplitude parcial, foco no reto abdominal."),
        (GrupoMuscularEnum.Core, "Elevação de Pernas", "Deitado, eleve as pernas estendidas até 90° para o reto inferior."),
        (GrupoMuscularEnum.Core, "Russian Twist", "Rotação do tronco com pés elevados, trabalho dos oblíquos."),
        (GrupoMuscularEnum.Core, "Abdominal com Roda", "Extensão abdominal com roda, trabalha todo o core."),
        (GrupoMuscularEnum.Core, "Prancha Lateral", "Isometria lateral para oblíquos e estabilizadores do tronco."),
        (GrupoMuscularEnum.Core, "Abdominal Bicicleta", "Alternância de cotovelo e joelho oposto em ritmo contínuo."),
        (GrupoMuscularEnum.Core, "Dead Bug", "Extensão alternada de braço e perna mantendo a lombar no chão."),
        (GrupoMuscularEnum.Core, "Oblíquo com Cabo", "Rotação de tronco lateral com resistência do cabo."),
        (GrupoMuscularEnum.Core, "Hollow Body", "Contração isométrica com corpo em forma de banana invertida."),

        (GrupoMuscularEnum.FullBody, "Burpee", "Combinação de flexão, agachamento e salto para alta demanda cardiovascular."),
        (GrupoMuscularEnum.FullBody, "Clean and Press com Barra", "Levantamento olímpico combinando power clean e desenvolvimento."),
        (GrupoMuscularEnum.FullBody, "Kettlebell Swing", "Balanço de kettlebell com explosão de quadril, trabalha cadeia posterior."),
        (GrupoMuscularEnum.FullBody, "Thruster com Barra", "Agachamento frontal seguido de desenvolvimento, alta intensidade."),
        (GrupoMuscularEnum.FullBody, "Turkish Get-Up", "Levantamento do chão à posição em pé com kettlebell acima da cabeça."),
        (GrupoMuscularEnum.FullBody, "Man Maker com Halteres", "Combinação de flexão, remada e clean and press com halteres."),
        (GrupoMuscularEnum.FullBody, "Devil Press", "Burpee com dois halteres finalizando em press acima da cabeça."),
        (GrupoMuscularEnum.FullBody, "Box Jump", "Salto para caixa elevada, desenvolve potência e explosão."),
        (GrupoMuscularEnum.FullBody, "Remada com Agachamento no Cabo", "Remada baixa combinada com agachamento para multi-articular."),
        (GrupoMuscularEnum.FullBody, "Deadlift Romeno com Remada", "Stiff seguido de remada curvada em único movimento contínuo."),
    ];

    private static readonly (TierPlanoEnum Tier, string Nome, int MaxAlunos, decimal Preco, bool Ativo, string? Descricao)[] PlanosPadrao =
    [
        (TierPlanoEnum.Free,    "Free",     10,  0m,   true,  "Ideal para começar e testar sem compromisso. Acesso à plataforma para até 10 alunos."),
        (TierPlanoEnum.Basic,   "Basic",    25,  50m,  true,  "Acesso completo à plataforma de treinos. R$2 por aluno/mês na lotação."),
        (TierPlanoEnum.Pro,     "Pro",      50,  100m, true,  "Tudo do Basic + notificações por e-mail que mantêm seus alunos engajados entre as sessões."),
        (TierPlanoEnum.ProPlus, "Pro Plus", 100, 200m, true,  "Tudo do Pro + WhatsApp integrado: seus alunos recebem tudo onde já estão."),
        (TierPlanoEnum.Elite,   "Elite",    300, 500m, false, "O plano mais completo: tudo do Pro Plus somado a IA para personalizar e otimizar cada treino."),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedGruposMuscularesAsync(cancellationToken).ConfigureAwait(false);
        await SeedExerciciosGlobaisAsync(cancellationToken).ConfigureAwait(false);
        await SeedPlanosPlataformaAsync(cancellationToken).ConfigureAwait(false);
        await SeedAdminAsync(cancellationToken).ConfigureAwait(false);
        await SeedZapTestUserAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedPlanosPlataformaAsync(CancellationToken cancellationToken)
    {
        var existentes = await context.PlanosPlataforma
            .Select(p => p.Tier)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var novos = PlanosPadrao
            .Where(p => !existentes.Contains(p.Tier))
            .Select(p =>
            {
                var plano = PlanoPlataforma.Criar(p.Nome, p.Tier, p.MaxAlunos, p.Preco, agora, p.Descricao).Value;
                if (!p.Ativo)
                    plano.Inativar(agora);
                return plano;
            })
            .ToList();

        if (novos.Count == 0)
            return;

        context.PlanosPlataforma.AddRange(novos);
        await context.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Planos de treinador criados: {Planos}", string.Join(", ", novos.Select(p => p.Nome)));
    }

    private async Task SeedGruposMuscularesAsync(CancellationToken cancellationToken)
    {
        var existentes = await context.GruposMusculares
            .Select(g => g.Nome)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var novos = GruposMuscularesPadrao
            .Where(n => !existentes.Contains(n))
            .Select(n => Domain.Entities.GrupoMuscular.Criar(n, agora).Value)
            .ToList();

        if (novos.Count == 0)
            return;

        context.GruposMusculares.AddRange(novos);
        await context.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Grupos musculares criados: {Grupos}", string.Join(", ", novos.Select(g => g.Nome)));
    }

    private async Task SeedExerciciosGlobaisAsync(CancellationToken cancellationToken)
    {
        var existentes = await context.Exercicios
            .Where(e => e.TreinadorId == null)
            .Select(e => e.Nome)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var gruposPorNome = await context.GruposMusculares
            .ToDictionaryAsync(g => g.Nome, g => g.Id, cancellationToken)
            .ConfigureAwait(false);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var novos = ExerciciosGlobais
            .Where(e => !existentes.Contains(e.Nome))
            .Select(e => Exercicio.Criar(e.Nome, gruposPorNome[e.Grupo.ToString()], agora, treinadorId: null, descricao: e.Descricao).Value)
            .ToList();

        if (novos.Count == 0)
            return;

        context.Exercicios.AddRange(novos);
        await context.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Exercícios globais criados: {Total}", novos.Count);
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        var jaExiste = await context.SystemUsers
            .AnyAsync(u => u.Role == SystemRole.SuperAdmin, cancellationToken)
            .ConfigureAwait(false);

        if (jaExiste)
            return;

        var email = configuration["Seed:AdminEmail"];
        if (string.IsNullOrWhiteSpace(email))
            email = "admin@forzion.tech";

        var senha = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(senha))
            throw new InvalidOperationException("Seed:AdminPassword não configurado.");

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var conta = Conta.Criar(Email.Criar(email).Value, passwordHasher.Hash(senha), TipoConta.SystemAdmin, agora).Value;
        conta.MarcarEmailVerificado(agora);
        conta.ClearDomainEvents();
        var systemUser = SystemUser.Criar(conta.Id, "Super Admin", agora).Value;

        context.Contas.Add(conta);
        context.SystemUsers.Add(systemUser);
        await context.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("SuperAdmin criado com sucesso.");
    }

    private async Task SeedZapTestUserAsync(CancellationToken cancellationToken)
    {
        if (environment.IsProduction())
            return;

        var senha = configuration["Seed:ZapTestPassword"];
        if (string.IsNullOrWhiteSpace(senha))
            return;

        var email = Email.Criar(configuration["Seed:ZapTestEmail"] ?? "zap-test@forzion.tech").Value;

        var jaExiste = await context.Contas
            .AnyAsync(c => c.Email == email, cancellationToken)
            .ConfigureAwait(false);

        if (jaExiste)
            return;

        var aprovadoPorId = await context.SystemUsers
            .Where(u => u.Role == SystemRole.SuperAdmin)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (aprovadoPorId == Guid.Empty)
            throw new InvalidOperationException("SeedZapTestUser exige SuperAdmin previamente semeado.");

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var conta = Conta.Criar(email, passwordHasher.Hash(senha), TipoConta.Treinador, agora).Value;
        conta.MarcarEmailVerificado(agora);
        conta.ClearDomainEvents();

        var treinador = Treinador.Criar(conta.Id, "ZAP DAST Test", agora).Value;
        treinador.Aprovar(aprovadoPorId, agora);
        treinador.ClearDomainEvents();

        context.Contas.Add(conta);
        context.Treinadores.Add(treinador);
        await context.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Conta de teste ZAP (DAST) criada: {Email}", email.Value);
    }
}
