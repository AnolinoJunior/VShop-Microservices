using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VShop.DiscountApi.DTOs;
using VShop.DiscountApi.Repositories;

namespace VShop.DiscountApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ApiScope")]
public class CouponController(ICouponRepository repository, IMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CouponDTO>>> GetAll(CancellationToken cancellationToken)
    {
        var coupons = await repository.GetAllAsync(cancellationToken);
        return Ok(mapper.Map<IEnumerable<CouponDTO>>(coupons));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CouponDTO>> GetById(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("O identificador do cupom deve ser positivo.");

        var coupon = await repository.GetByIdAsync(id, cancellationToken);
        return coupon is null ? NotFound() : Ok(mapper.Map<CouponDTO>(coupon));
    }

    [HttpGet("code/{code}")]
    public async Task<ActionResult<CouponDTO>> GetByCode(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 50)
            return BadRequest("O código do cupom deve ter entre 1 e 50 caracteres.");

        var coupon = await repository.GetByCodeAsync(code.Trim(), cancellationToken);
        return coupon is null ? NotFound() : Ok(mapper.Map<CouponDTO>(coupon));
    }

    [HttpGet("validate/{code}")]
    public async Task<ActionResult<CouponDTO>> Validate(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 50)
            return BadRequest("O código do cupom deve ter entre 1 e 50 caracteres.");

        var coupon = await repository.GetByCodeAsync(code.Trim(), cancellationToken);
        return coupon is { IsActive: true, DiscountAmount: > 0 }
            ? Ok(mapper.Map<CouponDTO>(coupon))
            : NotFound();
    }
}
