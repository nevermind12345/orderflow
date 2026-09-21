using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Restaurant.Infrastructure.Persistence
{
    public sealed class RestaurantDbContext(
    DbContextOptions<RestaurantDbContext> options)
    : DbContext(options);
}
