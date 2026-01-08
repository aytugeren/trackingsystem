using System.Threading.Tasks;
using KuyumculukTakipProgrami.Application.DTOs;

namespace KuyumculukTakipProgrami.Application.Interfaces;

public interface ITurmobInvoiceGateway
{
    Task<TurmobSendResult> SendAsync(TurmobInvoiceDto invoice);
}
