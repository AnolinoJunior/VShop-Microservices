namespace Vshop.IdentityServer.SeedDataBase
{
    public interface IDataBaseSeedInitializer
    {
        void InitializeSeedRoles();

        void InitializeSeedUsers();
    }
}