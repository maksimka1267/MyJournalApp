using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;

namespace MyJournalApp.Repository
{
    public class ClientRepository : Repository<Client>, IClientRepository
    {
        public ClientRepository(JournalDbContext context) : base(context)
        {
        }
    }
}
