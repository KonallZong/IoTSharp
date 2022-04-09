﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using IoTSharp.Controllers.Models;
using IoTSharp.Data;
using IoTSharp.Dtos;
using IoTSharp.Extensions;
using IoTSharp.Models;
using LinqKit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IoTSharp.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AssetController : ControllerBase
    {

        private readonly ApplicationDbContext _context;
        private readonly ILogger _logger;

        public AssetController(ApplicationDbContext context, ILogger<AssetController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ApiResult<PagedData<AssetDto>>> List([FromQuery] AssetParam m)
        {
            var profile = this.GetUserProfile();
            Expression<Func<Asset, bool>> condition = x =>
                x.Customer.Id == profile.Comstomer && x.Tenant.Id == profile.Tenant;


            if (!string.IsNullOrEmpty(m.Name))
            {
                condition = condition.And(x => x.Name.Contains(m.Name));
            }

            return new ApiResult<PagedData<AssetDto>>(ApiCode.Success, "OK", new PagedData<AssetDto>
            {
                total = await _context.Assets.CountAsync(condition),
                rows = _context.Assets.Where(condition).Where(condition).Skip((m.offset) * m.limit).Take(m.limit)
                    .ToList().Select(c => new AssetDto
                    { Id = c.Id, AssetType = c.AssetType, Description = c.Description, Name = c.Name }).ToList()

            });

        }

        [HttpGet]
        public ApiResult<PagedData<AssetDeviceItem>> Relations(Guid assetid)
        {

            var profile = this.GetUserProfile();

            var result = _context.Assets.Include(c => c.OwnedAssets)
                .SingleOrDefault(x =>
                    x.Id == assetid && x.Customer.Id == profile.Comstomer && x.Tenant.Id == profile.Tenant)?.OwnedAssets
                .ToList().GroupBy(c => c.DeviceId).Select(c => new
                {
                    Device = c.Key,
                    Attrs = c.Where(c => c.DataCatalog == DataCatalog.AttributeLatest).ToList(),
                    Temps = c.Where(c => c.DataCatalog == DataCatalog.TelemetryLatest).ToList()
                }
                ).ToList().Join(_context.Device, x => x.Device, y => y.Id, (x, y) => new AssetDeviceItem
                {
                    Id = x.Device,
                    Name = y.Name,
                    Online = y.Online,
                    LastActive = y.LastActive,
                    DeviceIdentity = y.DeviceIdentity,
                    DeviceType = y.DeviceType,
                    Status = y.Status,
                    Timeout = y.Timeout,
                    Attrs = x.Attrs.Select(c => new ModelAssetAttrItem
                    { dataSide = c.DataCatalog, keyName = c.KeyName, Name = c.Name, }).ToArray(),
                    Temps = x.Temps.Select(c => new ModelAssetAttrItem
                    { dataSide = c.DataCatalog, keyName = c.KeyName, Name = c.Name }).ToArray(),
                }).ToList();
            return new ApiResult<PagedData<AssetDeviceItem>>(ApiCode.Success, "OK",
                new PagedData<AssetDeviceItem>() { total = result?.Count ?? 0, rows = result }
            );

        }



        [HttpGet]
        public async Task<ApiResult<AssetDto>> Get(Guid id)
        {
            var profile = this.GetUserProfile();
            var asset = await _context.Assets.Include(c => c.Customer).Include(c => c.Tenant).SingleOrDefaultAsync(c =>
                c.Id == id && c.Customer.Id == profile.Comstomer && c.Tenant.Id == profile.Tenant);
            if (asset != null)
            {
                return new ApiResult<AssetDto>(ApiCode.Success, "OK",
                    new AssetDto()
                    {
                        AssetType = asset.AssetType,
                        Description = asset.Description,
                        Id = asset.Id,
                        Name = asset.Name
                    });
            }

            return new ApiResult<AssetDto>(ApiCode.CantFindObject, "Not found asset", null);

        }

        [HttpPut]
        public async Task<ApiResult<bool>> Update([FromBody] AssetDto dto)
        {

            var profile = this.GetUserProfile();
            var asset = await _context.Assets.Include(c => c.Customer).Include(c => c.Tenant).SingleOrDefaultAsync(c =>
                c.Id == dto.Id && c.Customer.Id == profile.Comstomer && c.Tenant.Id == profile.Tenant);
            if (asset == null)
            {

                return new ApiResult<bool>(ApiCode.CantFindObject, "Not found asset", false);
            }

            try
            {
                asset.AssetType = dto.AssetType;
                asset.Name = dto.Name;
                asset.Description = dto.Description;
                _context.Assets.Update(asset);
                await _context.SaveChangesAsync();

                return new ApiResult<bool>(ApiCode.Success, "Ok", true);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex.Message);
                return new ApiResult<bool>(ApiCode.Exception, "error", false);
            }

        }


        [HttpPost]
        public async Task<ApiResult<bool>> Save([FromBody] AssetDto dto)
        {
            try
            {

                var profile = this.GetUserProfile();
                Asset asset = new Asset();
                asset.Tenant = _context.Tenant.SingleOrDefault(c => c.Id == profile.Tenant);
                asset.Customer = _context.Customer.SingleOrDefault(c => c.Id == profile.Comstomer);
                asset.AssetType = dto.AssetType;
                asset.Name = dto.Name;
                asset.Description = dto.Description;
                _context.Assets.Add(asset);
                await _context.SaveChangesAsync();
                return new ApiResult<bool>(ApiCode.Success, "Ok", true);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex.Message);
                return new ApiResult<bool>(ApiCode.Exception, "error", false);
            }


        }

        [HttpDelete]
        public async Task<ApiResult<bool>> Delete(Guid id)
        {

            var profile = this.GetUserProfile();
            try
            {
                var asset = await _context.Assets.Include(c => c.Customer).Include(c => c.Tenant)
                    .Include(c => c.OwnedAssets).SingleOrDefaultAsync(c =>
                        c.Id == id && c.Customer.Id == profile.Comstomer && c.Tenant.Id == profile.Tenant);
                if (asset == null)
                {

                    return new ApiResult<bool>(ApiCode.CantFindObject, "Not found asset", false);
                }

                _context.Assets.Remove(asset);
                await _context.SaveChangesAsync();
                return new ApiResult<bool>(ApiCode.Success, "Ok", true);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex.Message);
                return new ApiResult<bool>(ApiCode.Exception, "error", false);
            }

        }


        [HttpPost]
        public async Task<ApiResult<bool>> addDevice(ModelAssetDevice m)
        {

            var profile = this.GetUserProfile();
            try
            {
                var asset = await _context.Assets.Include(c => c.Customer).Include(c => c.Tenant)
                    .Include(c => c.OwnedAssets).SingleOrDefaultAsync(c =>
                        c.Id == m.AssetId && c.Customer.Id == profile.Comstomer && c.Tenant.Id == profile.Tenant);
                if (asset == null)
                {

                    return new ApiResult<bool>(ApiCode.CantFindObject, "Not found asset", false);
                }

                foreach (var item in m.Attrs)
                {
                    if (asset.OwnedAssets.All(c => c.KeyName != item.keyName))
                    {
                        asset.OwnedAssets.Add(new AssetRelation()
                        {
                            DeviceId = m.Deviceid,
                            DataCatalog = DataCatalog.AttributeLatest,
                            Description = "",
                            KeyName = item.keyName,
                            Name = item.keyName,
                        });
                    }

                }

                foreach (var item in m.Temps)
                {
                    if (asset.OwnedAssets.All(c => c.KeyName != item.keyName))
                    {
                        asset.OwnedAssets.Add(new AssetRelation()
                        {
                            DeviceId = m.Deviceid,
                            DataCatalog = DataCatalog.TelemetryLatest,
                            Description = "",
                            KeyName = item.keyName,
                            Name = item.keyName
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return new ApiResult<bool>(ApiCode.Success, "Ok", true);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex.Message);
                return new ApiResult<bool>(ApiCode.Exception, "error", false);
            }


        }




        [HttpDelete]
        public async Task<ApiResult<bool>> RemoveDevice(ModelAssetDevice m)
        {

            var profile = this.GetUserProfile();
            try
            {
                var asset = await _context.Assets.Include(c => c.Customer).Include(c => c.Tenant)
                    .Include(c => c.OwnedAssets).SingleOrDefaultAsync(c =>
                        c.Id == m.AssetId && c.Customer.Id == profile.Comstomer && c.Tenant.Id == profile.Tenant);

                await _context.SaveChangesAsync();
                return new ApiResult<bool>(ApiCode.Success, "Ok", true);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex.Message);
                return new ApiResult<bool>(ApiCode.Exception, "error", false);
            }

        }

        [HttpDelete]

        public async Task<ApiResult<bool>> RemoveAssetAttr(Guid assetId, Guid deviceid, string Keyname)
        {
            var profile = this.GetUserProfile();
            try
            {
                var asset = await _context.Assets.Include(c => c.Customer).Include(c => c.Tenant)
                     .Include(c => c.OwnedAssets).SingleOrDefaultAsync(c =>
                         c.Id == assetId && c.Customer.Id == profile.Comstomer && c.Tenant.Id == profile.Tenant);

                var attr = asset.OwnedAssets.FirstOrDefault(c =>
                      c.DeviceId == deviceid && c.DataCatalog == DataCatalog.AttributeLatest && c.KeyName == Keyname);
                if (attr != null)
                {
                    asset.OwnedAssets.Remove(attr);
                    await _context.SaveChangesAsync(); return new ApiResult<bool>(ApiCode.Success, "Ok", true);
                }

                return new ApiResult<bool>(ApiCode.Success, "can't find this attribute", false);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex.Message);
                return new ApiResult<bool>(ApiCode.Exception, "error", false);
            }
        }




        [HttpDelete]

        public async Task<ApiResult<bool>> RemoveAssetTemp(Guid assetId, Guid deviceid, string Keyname)
        {
            var profile = this.GetUserProfile();
            try
            {
                var asset = await _context.Assets.Include(c => c.Customer).Include(c => c.Tenant)
                    .Include(c => c.OwnedAssets).SingleOrDefaultAsync(c =>
                        c.Id == assetId && c.Customer.Id == profile.Comstomer && c.Tenant.Id == profile.Tenant);

                var attr = asset.OwnedAssets.FirstOrDefault(c =>
                    c.DeviceId == deviceid && c.DataCatalog == DataCatalog.AttributeLatest && c.KeyName == Keyname);
                if (attr != null)
                {
                    asset.OwnedAssets.Remove(attr);
                    await _context.SaveChangesAsync(); return new ApiResult<bool>(ApiCode.Success, "Ok", true);
                }

                return new ApiResult<bool>(ApiCode.Success, "can't find this attribute", false);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex.Message);
                return new ApiResult<bool>(ApiCode.Exception, "error", false);
            }
        }
    }
} 
