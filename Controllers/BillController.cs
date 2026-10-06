using Hotel_Management_Website.Filters;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.Bill;
using Hotel_Management_Website.Models.ViewModels.UI;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace Hotel_Management_Website.Controllers;

[ReceptionAccess]
public sealed class BillController : Controller
{
    private readonly IBillService _billService;
    private readonly ILogger<BillController> _logger;

    public BillController(
        IBillService billService,
        ILogger<BillController> logger)
    {
        _billService = billService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        BillIndexQueryViewModel query,
        CancellationToken cancellationToken)
    {
        query ??= new BillIndexQueryViewModel();
        query.Filter ??= new BillListFilterViewModel();

        if (string.Equals(query.Reset, "bill", StringComparison.OrdinalIgnoreCase))
        {
            query.Filter = new BillListFilterViewModel();
        }

        try
        {
            var model = await _billService.GetIndexAsync(
                query.Filter,
                cancellationToken);

            ApplyPaginationUrl(model);
            return View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải danh sách hóa đơn.");

            return View(new BillIndexViewModel
            {
                Filter = new BillListFilterViewModel
                {
                    CurrentPage = 1,
                    PageSize = 10
                },
                Bills = Array.Empty<BillListItemViewModel>(),
                Pagination = new PaginationViewModel
                {
                    CurrentPage = 1,
                    PageSize = 10,
                    TotalItems = 0,
                    PageParam = "Filter.CurrentPage",
                    PageSizeParam = "Filter.PageSize",
                    BaseUrl = "/Bill/Index",
                    ItemLabel = "hóa đơn"
                },
                LoadError = "Không thể tải danh sách hóa đơn. Vui lòng thử lại."
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Print(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        try
        {
            var model = await _billService.GetDetailsAsync(
                id,
                cancellationToken);

            if (model is null)
            {
                return NotFound();
            }

            return View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lỗi khi tải hóa đơn để in {BillId}.",
                id);

            TempData["ErrorMessage"] =
                "Không thể tải hóa đơn để in. Vui lòng thử lại.";

            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        try
        {
            var model = await _billService.GetDetailsAsync(
                id,
                cancellationToken);

            if (model is null)
            {
                return NotFound();
            }

            return View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lỗi khi tải chi tiết hóa đơn {BillId}.",
                id);

            TempData["ErrorMessage"] =
                "Không thể tải chi tiết hóa đơn. Vui lòng thử lại.";

            return RedirectToAction(nameof(Index));
        }
    }

    private void ApplyPaginationUrl(BillIndexViewModel model)
    {
        var baseUrl = Url.Action(nameof(Index), "Bill") ?? "/Bill/Index";

        model.Pagination = new PaginationViewModel
        {
            CurrentPage = model.Pagination.CurrentPage,
            PageSize = model.Pagination.PageSize,
            TotalItems = model.Pagination.TotalItems,
            PageParam = model.Pagination.PageParam,
            PageSizeParam = model.Pagination.PageSizeParam,
            BaseUrl = baseUrl,
            ItemLabel = model.Pagination.ItemLabel,
            QueryParameters = new Dictionary<string, string?>
            {
                ["Filter.Search"] = model.Filter.Search,
                ["Filter.Sort"] = ((byte)model.Filter.Sort)
                    .ToString(CultureInfo.InvariantCulture),
                ["Filter.Status"] = model.Filter.Status?.ToString(CultureInfo.InvariantCulture),
                ["Filter.CurrentPage"] = model.Filter.CurrentPage
                    .ToString(CultureInfo.InvariantCulture),
                ["Filter.PageSize"] = model.Filter.PageSize
                    .ToString(CultureInfo.InvariantCulture)
            }
        };
    }
}
