using LinqToDB;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Contexts;
using NuclearEvaluation.Kernel.Helpers;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace NuclearEvaluation.SharedServices.Services;

public class PmiReportService : DbServiceBase
{
    public PmiReportService(NuclearEvaluationServerDbContext dbContext) : base(dbContext)
    {

    }

    public async Task Insert(PmiReport pmiReport)
    {
        using TransactionScope ts = TransactionProvider.CreateScope();

        using DataConnection dc = _dbContext.CreateLinqToDBConnection();

        await dc.InsertAsync(pmiReport);
    }

    public async Task QueueForDistributionAsync(PmiReport pmiReport)
    {

    }
}
