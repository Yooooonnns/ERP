using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DigitalisationERP.Domain.Entities;
using DigitalisationERP.Application.Interfaces;

namespace DigitalisationERP.Application.Services
{
    /// <summary>
    /// Service de gestion des lignes de production
    /// </summary>
    public class ProductionLineService : IProductionLineService
    {
        // Mock pour démo
        private static readonly List<ProductionLine> _lines = new();
        private static readonly List<ProductionPost> _posts = new();
        private static int _nextLineId = 1;
        private static int _nextPostId = 1;

        public async Task<Result<ProductionLine>> CreateLineAsync(
            int userId,
            string lineName,
            string description,
            double minAvailability,
            double minPerformance,
            double minQuality)
        {
            await Task.Delay(100);

            if (string.IsNullOrWhiteSpace(lineName))
                return Result<ProductionLine>.Fail("Line name is required");

            var line = new ProductionLine
            {
                LineId = _nextLineId++,
                UserId = userId,
                LineName = lineName,
                Description = description,
                MinAvailability = minAvailability,
                MinPerformance = minPerformance,
                MinQuality = minQuality,
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            };

            _lines.Add(line);
            return Result<ProductionLine>.Ok(line, "Production line created successfully");
        }

        public async Task<ProductionLine?> GetUserLineAsync(int userId)
        {
            await Task.Delay(50);
            return _lines.FirstOrDefault(l => l.UserId == userId && l.IsActive);
        }

        public async Task<Result<ProductionPost>> AddPostAsync(
            int lineId,
            string postCode,
            string postName,
            int position,
            string postType,
            int targetCadence,
            int maxCapacity)
        {
            await Task.Delay(100);

            var line = _lines.FirstOrDefault(l => l.LineId == lineId);
            if (line == null)
                return Result<ProductionPost>.Fail("Production line not found");

            var post = new ProductionPost
            {
                PostId = _nextPostId++,
                LineId = lineId,
                PostCode = postCode,
                PostName = postName,
                Position = position,
                PostType = postType,
                TargetCadence = targetCadence,
                MaxCapacity = maxCapacity,
                Status = "Offline",
                LastStatusChange = DateTime.UtcNow,
                CurrentStock = 0,
                MaintenanceHealthScore = 100
            };

            _posts.Add(post);
            line.Posts.Add(post);

            return Result<ProductionPost>.Ok(post, "Post added successfully");
        }

        public async Task<List<ProductionPost>> GetLinePostsAsync(int lineId)
        {
            await Task.Delay(50);
            return _posts.Where(p => p.LineId == lineId).OrderBy(p => p.Position).ToList();
        }
    }
}
