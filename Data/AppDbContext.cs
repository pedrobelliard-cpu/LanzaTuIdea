using LanzaTuIdea.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LanzaTuIdea.Api.Data;

public class AppDbContext : DbContext
{
    private readonly bool _useEmployeeView;

    public AppDbContext(DbContextOptions<AppDbContext> options, IConfiguration configuration)
        : base(options)
    {
        _useEmployeeView = configuration.GetValue<bool>("Database:UseEmployeeView");
    }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Idea> Ideas => Set<Idea>();
    public DbSet<IdeaHistory> IdeaHistories => Set<IdeaHistory>();
    public DbSet<Classification> Classifications => Set<Classification>();
    public DbSet<Instance> Instances => Set<Instance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Codigo_Empleado);
            entity.Property(e => e.Codigo_Empleado).HasMaxLength(20);
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.Apellido1).HasMaxLength(100);
            entity.Property(e => e.Apellido2).HasMaxLength(100);
            entity.Property(e => e.E_Mail).HasMaxLength(200);
            entity.Property(e => e.Departamento).HasMaxLength(200);
            entity.Property(e => e.Estatus).HasMaxLength(5).HasDefaultValue("A");

            if (_useEmployeeView)
            {
                entity.ToView("vw_Employees");
            }
            else
            {
                entity.ToTable("Employees");
            }
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(u => u.UserName).IsUnique();
            entity.Property(u => u.UserName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Codigo_Empleado).HasMaxLength(20);
            entity.Property(u => u.NombreCompleto).HasMaxLength(200);
            entity.Property(u => u.Instancia).HasMaxLength(200);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(r => r.Name).IsUnique();
            entity.Property(r => r.Name).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });
            entity.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);
            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);
        });

        modelBuilder.Entity<Idea>(entity =>
        {
            entity.Property(i => i.CodigoEmpleado).HasMaxLength(20).IsRequired();
            entity.Property(i => i.Descripcion).HasMaxLength(500).IsRequired();
            entity.Property(i => i.Detalle).HasMaxLength(4000).IsRequired();
            entity.Property(i => i.Status).HasMaxLength(50).IsRequired();
            entity.Property(i => i.Clasificacion).HasMaxLength(200);
            entity.Property(i => i.Via).HasMaxLength(100);
            entity.Property(i => i.AdminComment).HasMaxLength(1000);
            entity.HasOne(i => i.CreatedByUser)
                .WithMany(u => u.Ideas)
                .HasForeignKey(i => i.CreatedByUserId);
        });

        modelBuilder.Entity<IdeaHistory>(entity =>
        {
            entity.Property(h => h.ChangeType).HasMaxLength(200).IsRequired();
            entity.Property(h => h.Notes).HasMaxLength(1000);
            entity.HasOne(h => h.Idea)
                .WithMany(i => i.History)
                .HasForeignKey(h => h.IdeaId);
            entity.HasOne(h => h.ChangedByUser)
                .WithMany()
                .HasForeignKey(h => h.ChangedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Classification>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Nombre).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Instance>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Nombre).HasMaxLength(200).IsRequired();
        });
    }

    public override int SaveChanges()
    {
        if (_useEmployeeView)
        {
            IgnoreEmployeeWrites();
        }
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_useEmployeeView)
        {
            IgnoreEmployeeWrites();
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    private void IgnoreEmployeeWrites()
    {
        foreach (var entry in ChangeTracker.Entries<Employee>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                entry.State = EntityState.Unchanged;
            }
        }
    }
}
