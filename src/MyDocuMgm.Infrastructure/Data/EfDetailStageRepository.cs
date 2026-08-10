using Microsoft.EntityFrameworkCore;
using MyDocuMgm.Application;
using MyDocuMgm.Application.Contents.ReviewDetail;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Infrastructure.Data;

public sealed class EfDetailStageRepository(MyDocuMgmDbContext dbContext) : IDetailStageRepository
{
    public Task<Content?> FindAsync(Guid contentId, CancellationToken cancellationToken) =>
        dbContext.Contents
            .AsSplitQuery()
            .Include(content => content.PlaceDetails)
            .Include(content => content.CookingDetails).ThenInclude(details => details!.Ingredients)
            .Include(content => content.ExerciseDetails)
            .Include(content => content.CleaningLaundryDetails)
            .Include(content => content.TravelDetails)
            .Include(content => content.PhotoDetails)
            .Include(content => content.StudyDetails)
            .Include(content => content.ProductDetails)
            .Include(content => content.PhoneComputerDetails)
            .Include(content => content.TipDetails)
            .Include(content => content.OtherDetails)
            .Include(content => content.LinkedMedia).ThenInclude(link => link.MediaAsset)
            .SingleOrDefaultAsync(content => content.Id == contentId, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException($"다른 변경이 먼저 저장되었습니다: {exception.Message}");
        }
    }
}
