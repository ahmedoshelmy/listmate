using FluentValidation;
using Listmate.Domain.Items;

namespace Listmate.Application.Sync;

public sealed class SyncRequestValidator : AbstractValidator<SyncRequest>
{
    public const int MaxBatch = 500;

    public SyncRequestValidator()
    {
        RuleFor(r => r.Mutations).NotNull().Must(m => m.Count is > 0 and <= MaxBatch)
            .WithMessage($"Send between 1 and {MaxBatch} mutations.");
        RuleForEach(r => r.Mutations).SetValidator(new MutationValidator());
    }

    private sealed class MutationValidator : AbstractValidator<MutationDto>
    {
        public MutationValidator()
        {
            RuleFor(m => m.MutationId).NotEmpty();
            RuleFor(m => m.ListId).NotEmpty();
            RuleFor(m => m.ItemId).NotEmpty();
            RuleFor(m => m.Type).Must(t => t is not null && MutationTypes.All.Contains(t))
                .WithMessage("Type must be add, edit, check or delete.");
            RuleFor(m => m.BaseVersion).GreaterThanOrEqualTo(0);

            When(m => m.Type == MutationTypes.Add, () =>
                RuleFor(m => m.Text).NotEmpty().MaximumLength(ItemText.MaxNameLength + 8));
            When(m => m.Type == MutationTypes.Edit, () =>
            {
                RuleFor(m => m).Must(m => m.Name is not null || m.Quantity is not null)
                    .WithMessage("An edit sets a name, a quantity or both.");
                RuleFor(m => m.Name).NotEmpty().MaximumLength(ItemText.MaxNameLength).When(m => m.Name is not null);
                RuleFor(m => m.Quantity).InclusiveBetween(1, ItemText.MaxQuantity);
            });
            When(m => m.Type == MutationTypes.Check, () => RuleFor(m => m.Checked).NotNull());
        }
    }
}
