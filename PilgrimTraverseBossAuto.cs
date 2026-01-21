using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Module.GameEvent.Types;
using KodakkuAssist.Extensions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;
using System.Linq;
using System.ComponentModel;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;
using Lumina.Excel.Sheets;

namespace KodakkuScripts.UsamisPrivateScript._07_Dawntrail.PilgrimTraverse;


[ScriptType(name: Name, territorys: [1281, 1282, 1283, 1284, 1285, 1286, 1287, 1288, 1289, 1290, 1333], guid: "fb8c4010-82cf-4d35-9ce8-c80b58215a9d",
    version: Version, author: "Usami", note: NoteStr, updateInfo: UpdateInfo)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|钐镰客|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*35501.*$
// ^\[\w+\|[^|]+\|E\]\s\w+

public class PilgrimTraverseBossAuto
{
    const string NoteStr =
        $"""
        {Version}
        与MazeClear配合实现的妖宫boss辅助，
        用当前可支配的手段解决一些BMR不太理想的行动。
        单人模式限定，配合I-ching、vnavmesh、BMR使用。
        """;
    
    const string UpdateInfo =
        $"""
         {Version}
         F50 不准跑快快
         """;

    private const string Name = "MazeClear妖宫Boss辅助";
    private const string Version = "0.0.0.6";
    private const string DebugVersion = "a";
    private const bool Debugging = false;
    private bool _enable = true;

    private static BossStateParams _bsp = new();
    
    public void Init(ScriptAccessory sa)
    {
        sa.Method.ClearFrameworkUpdateAction(this);
        sa.Method.RemoveDraw(".*");
        RefreshParams(sa);
        if (sa.Data.PartyList.Count > 1)
        {
            sa.DebugMsg($"检测到非单人模式，BossAuto辅助关闭");
            _enable = false;
        }
        else
        {
            sa.DebugMsg($"检测到单人模式，BossAuto辅助开启");
            _enable = true;
        }
    }
    
    private void RefreshParams(ScriptAccessory sa)
    {
        _bsp = new BossStateParams();
    }

    [ScriptMethod(name: "———————— 《测试项》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 测试项分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    
    [ScriptMethod(name: "bsp检测", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void bsp检测(Event ev, ScriptAccessory sa)
    {
        var hpPercentMan = (float)((IBattleChara)_bsp.F99_DarkManObj).CurrentHp / _bsp.F99_DarkManHpMax;
        var hpPercentWoman = (float)((IBattleChara)_bsp.F99_LightWomanObj).CurrentHp / _bsp.F99_LightWomanHpMax;
            
        // 倾向于每5%进行一次更换
        var p = hpPercentMan - hpPercentWoman;
        sa.DebugMsg($"血量差：{p}");
        sa.DebugMsg($"我的当前状态：{_bsp.F99_buffState}，下一个状态：{_bsp.F99_nextBuffState}");
        sa.DebugMsg($"切换buff空闲：{_bsp.F99_changeBuffIdle}，能否切换buff：{_bsp.F99_buffStateExchangeEnable}");
    }
    
    #region F10 花人
    
    [ScriptMethod(name: "———————— 《F10 花人》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void F10_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "分株读条至场中关闭BMR", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44051"],
        userControl: true)]
    public void 分株读条关闭BMR(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        var center = new Vector3(-300, 0, -300);
        // sa.DrawGuidance(new Vector3(-300, 0, -300), 0, 3000, $"分株去场中");
        SwitchAiMode(sa, false);
        MoveTo(sa, center);
        sa.DebugMsg($"分株读条，关闭BMR，移动至场中", Debugging);
    }
    
    [ScriptMethod(name: "百花齐放移动", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44855"],
        userControl: true)]
    public void 百花齐放移动(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        var center = new Vector3(-300, 0, -300);
        var region = ev.SourcePosition.GetRadian(center).RadianToRegion(8, isDiagDiv: true);
        lock (_bsp)
        {
            _bsp.F10A_castRegionVal += 1 << region;
            _bsp.F10A_castCount++;
            if (_bsp.F10A_castCount < 3) return;
            
            // 获得某一位，要求该位及前后均为0
            for (var i = 0; i < 8; i++)
            {
                if (_bsp.F10A_castRegionVal.GetBinaryBit(i) != 0) continue;
                
                var prev = i == 0 ? 7 : i - 1;
                var next = i == 7 ? 0 : i + 1;
                if (_bsp.F10A_castRegionVal.GetBinaryBit(prev) != 0 || _bsp.F10A_castRegionVal.GetBinaryBit(next) != 0) continue;

                var safeRegionRadian = i * 45f.DegToRad();
                var safePos = new Vector3(-300, 0, -285).RotateAndExtend(center, safeRegionRadian);
                
                // sa.DrawGuidance(safePos, 0, 10000, $"百花齐放安全点");
                MoveTo(sa, safePos);
                
                break;
            }
        }
    }

    [ScriptMethod(name: "百花齐放施法结束回中", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:44854"],
        userControl: true)]
    public void 百花齐放施法结束回中(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        var center = new Vector3(-300, 0, -300);
        MoveTo(sa, center);
        _bsp.Reset(sa, 10);
    }

    [ScriptMethod(name: "压花移动第一轮", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:44058"],
        userControl: true)]
    public void 压花移动第一轮(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        lock (_bsp)
        {
            var center = new Vector3(-300, 0, -300);
            var region = ev.EffectPosition.GetRadian(center).RadianToRegion(8, isDiagDiv: true);
            
            _bsp.F10B_castRegionVal += region * (int)Math.Pow(10, _bsp.F10B_castCount);
            _bsp.F10B_castCount++;
            if (_bsp.F10B_castCount < 4) return;
            
            // 判断四穿一/三穿一
            // 第四处的安全区与第一处的安全区呈对角关系，则选三穿一
            var threeToOne = Math.Abs(_bsp.F10B_castRegionVal.GetDecimalDigit(3) - _bsp.F10B_castRegionVal.GetDecimalDigit(0)) == 4;
            
            var safePosRadian = _bsp.F10B_castRegionVal.GetDecimalDigit(threeToOne ? 2 : 3) * 45f.DegToRad();
            var basePos = new Vector3(-300, 0, -300 + 8.5f);
            var safePos = basePos.RotateAndExtend(center, safePosRadian);
            // sa.DrawGuidance(safePos, 0, 2000, $"压花四穿一准备");
            MoveTo(sa, safePos);
        }
    }
    
    [ScriptMethod(name: "压花移动第二轮", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:44057", "TargetIndex:1"],
        userControl: true)]
    public void 压花移动第二轮(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        lock (_bsp)
        {
            _bsp.F10B_castCount++;

            if (_bsp.F10B_castCount == 5)
            {
                // 去第二个位置待命
                var center = new Vector3(-300, 0, -300);
                var safePosRadian = _bsp.F10B_castRegionVal.GetDecimalDigit(0) * 45f.DegToRad();
                var basePos = new Vector3(-300, 0, -300 + 8.5f);
                var safePos = basePos.RotateAndExtend(center, safePosRadian);
                // sa.DrawGuidance(safePos, 0, 4000, $"压花四穿一");
                MoveTo(sa, safePos);
            }

            if (_bsp.F10B_castCount == 8)
            {
                _bsp.Reset(sa, 10);
                SwitchAiMode(sa, true);
            }
        }
    }
    
    #endregion F10 花人

    #region F20 得到宽恕的欧米茄
    
    [ScriptMethod(name: "———————— 《F20 得到宽恕的欧米茄》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void F20_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "空降施法回中开防击退", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43454"],
        userControl: true)]
    public void 空降施法回中开防击退(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        var center = new Vector3(-300, 0, -300);
        
        SwitchAiMode(sa, false);
        MoveTo(sa, center);
        SwitchAntiKnockback(sa, true);
        sa.DebugMsg($"空降施法，开启I-ching防击退，关闭BMR，回中", Debugging);
    }
    
    [ScriptMethod(name: "空降移动第一轮", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2056", "StackCount:regex:^(149|15[012])$"],
        userControl: true)]
    public void 空降移动第一轮(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        lock (_bsp)
        {
            var region = ev.StatusStackCount switch
            {
                149 => 3,
                150 => 5,
                151 => 1,
                152 => 7,
                _ => 0
            };
            _bsp.F20A_castRegionVal += region * (int)Math.Pow(10, _bsp.F20A_castCount);
            _bsp.F20A_castCount++;
            
            if (_bsp.F20A_castCount < 4) return;
            
            var center = new Vector3(-300, 0, -300);
            // 去第四个位置待命
            var safePosRadian = _bsp.F20A_castRegionVal.GetDecimalDigit(3) * 45f.DegToRad();
            var basePos = new Vector3(-300, 0, -300 + 7.5f);
            var safePos = basePos.RotateAndExtend(center, safePosRadian);
            // sa.DrawGuidance(safePos, 0, 2000, $"空降四穿一准备");
            MoveTo(sa, safePos);
        }
    }
    
    [ScriptMethod(name: "空降移动第二轮", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:43456", "TargetIndex:1"],
        userControl: true)]
    public void 空降移动第二轮(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        // 去第二个位置待命
        var center = new Vector3(-300, 0, -300);
        var safePosRadian = _bsp.F20A_castRegionVal.GetDecimalDigit(0) * 45f.DegToRad();
        var basePos = new Vector3(-300, 0, -300 + 7.5f);
        var safePos = basePos.RotateAndExtend(center, safePosRadian);
        // sa.DrawGuidance(safePos, 0, 4000, $"空降四穿一");
        MoveTo(sa, safePos);
    }
    
    [ScriptMethod(name: "空降第四轮结束", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:43459", "TargetIndex:1"],
        userControl: true)]
    public void 空降第四轮结束(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        SwitchAiMode(sa, true);
        _bsp.Reset(sa, 20);
        SwitchAntiKnockback(sa, false);
        sa.DebugMsg($"空降结束，关闭I-ching防击退，开启BMR", Debugging);
    }
    
    #endregion F20 得到宽恕的欧米茄

    #region F30 得到宽恕的不规则场地

    [ScriptMethod(name: "———————— 《F30 得到宽恕的不规则场地》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void F30_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "落光施法起跑准备", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44917"],
        userControl: true)]
    public void 落光施法起跑准备(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        var center = new Vector3(-300, 0, -300);
        SwitchAiMode(sa, false);

        // 根据所在区域前往起跑点
        var region = sa.Data.MyObject.Position.GetRadian(center).RadianToRegion(12, 0, true);
        _bsp.F30A_playerTargetRegion = region;
        var basePos = new Vector3(-300, 0, -283);
        // 十二等分，360 / 12 = 30
        var startPos = basePos.RotateAndExtend(center, region * 30f.DegToRad());
        // sa.DrawGuidance(startPos, 0, 4000, $"落光起跑点");
        MoveTo(sa, startPos);
        sa.DebugMsg($"落光施法，当前区域为 {region}，前往起跑点，关闭BMR", Debugging);
    }
    
    [ScriptMethod(name: "落光开始跑地火", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44918"],
        userControl: true)]
    public void 落光开始跑地火(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        var center = new Vector3(-300, 0, -300);
        sa.Method.RemoveDraw($"落光起跑点");
        _bsp.F30A_frameWorkAction = sa.Method.RegistFrameworkUpdateAction(Action);

        void Action()
        {
            var region = sa.Data.MyObject.Position.GetRadian(center).RadianToRegion(12, 0, true);
            if (region != _bsp.F30A_playerTargetRegion) return;
            
            // 到达区域，前往下一个
            sa.Method.RemoveDraw($"落光目标点{region}");
            var nextRegion = (region + 1) % 12;
            var basePos = new Vector3(-300, 0, -283);
            var targetPos = basePos.RotateAndExtend(center, nextRegion * 30f.DegToRad());
            _bsp.F30A_playerTargetRegion = nextRegion;
            // sa.DrawGuidance(targetPos, 0, 4000, $"落光目标点{nextRegion}");
            MoveTo(sa, targetPos);
            sa.DebugMsg($"前往下一个区域 {region}", Debugging);
        }
    }

    [ScriptMethod(name: "跑地火结束", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:39487"],
        userControl: true)]
    public void 跑地火结束(Event ev, ScriptAccessory sa)
    {
        // 光耀颂词读条完毕
        if (!_enable) return;
        if (_bsp.F30A_frameWorkAction == "") return;
        sa.Method.UnregistFrameworkUpdateAction(_bsp.F30A_frameWorkAction);
        _bsp.Reset(sa, 30);
        MoveStop(sa);
        SwitchAiMode(sa, true);
        sa.DebugMsg($"跑地火结束，开启BMR", Debugging);
    }

    #endregion F30 得到宽恕的哈迪斯
    
    #region F50 奥格巴拉巴拉
    
    [ScriptMethod(name: "———————— 《F50 奥格巴拉巴拉》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void F50_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "进沙坑读条关闭BMR起跑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43533"],
        userControl: true)]
    public void 进沙坑读条起跑准备(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        SwitchAiMode(sa, false);
        sa.DebugMsg($"进沙坑读条，关闭BMR", Debugging);
        SetSpeed(sa, 1);
        var myPos = sa.Data.MyObject.Position;

        var minDistance = 40f;
        var minSpot = _bsp.F50A_safeSpots[0];
        // 找起跑点
        for (int i = 0; i < 6; i++)
        {
            var spot = _bsp.F50A_safeSpots[i];
            var distance = Vector3.Distance(myPos.WithY(0), spot.WithY(0));
            if (distance < minDistance)
            {
                minDistance = distance;
                minSpot = spot;
                _bsp.F50A_targetSpotIdx = i;
            }
            // 3代表在当前小石头上
            if (distance > 3f) continue;
            // sa.DrawGuidance(spot, 0, 4000, $"沙坑起跑点");
            MoveTo(sa, spot);
            return;
        }
        // 若未在给定的小石头上，去离自己最近的那个
        // sa.DrawGuidance(minSpot, 0, 4000, $"沙坑起跑点");
        MoveTo(sa, minSpot);
    }

    [ScriptMethod(name: "测试项-TargetSpot", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43534"],
        userControl: Debugging)]
    public void 测试项TargetSpot(Event ev, ScriptAccessory sa)
    {
        
        var myPos = sa.Data.MyObject.Position;
        var targetSpot = _bsp.F50A_safeSpots[_bsp.F50A_targetSpotIdx];
        sa.DrawGuidance(targetSpot, 0, 2000, $"a");
        var distance = Vector3.Distance(myPos.WithY(0), targetSpot.WithY(0));
        sa.DebugMsg($"{targetSpot}, {distance}");
    }

    [ScriptMethod(name: "破坑而出开始", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:43534"],
        userControl: true)]
    public void 破坑而出开始(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        // 破坑而出百分百点自己，这里默认顺时针跑
        _bsp.F50A_frameWorkAction = sa.Method.RegistFrameworkUpdateAction(Action);

        void Action()
        {
            var myPos = sa.Data.MyObject.Position;
            var targetSpot = _bsp.F50A_safeSpots[_bsp.F50A_targetSpotIdx];
            var distance = Vector3.Distance(myPos.WithY(0), targetSpot.WithY(0));
            
            // 还没到则返回
            if (distance > 1.5f) return;
            // 别再跑了
            if (_bsp.F50A_spotCount >= 4) return;
            
            if (_bsp.F50A_spotCount != 0 && !_bsp.F50A_keepRunning) return;
            // 跑到下一个点
            sa.Method.RemoveDraw($"沙坑目标{_bsp.F50A_targetSpotIdx}");
            var nextSpotIdx = (_bsp.F50A_targetSpotIdx + 1) % 6;
            // var nextSpotIdx = (_bsp.F50A_targetSpotIdx + 6 - 1) % 6;
            var nextTargetSpot = _bsp.F50A_safeSpots[nextSpotIdx];
            _bsp.F50A_targetSpotIdx = nextSpotIdx;
            _bsp.F50A_spotCount++;
            // sa.DrawGuidance(nextTargetSpot, 0, 4000, $"沙坑目标{nextSpotIdx}");
            MoveTo(sa, nextTargetSpot);
        }
    }
    
    [ScriptMethod(name: "破坑而出继续奔跑", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:43534"],
        userControl: true)]
    public void 破坑而出继续奔跑(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        _bsp.F50A_keepRunning = true;
    }
    
    [ScriptMethod(name: "破坑而出结束", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:43536"],
        userControl: true)]
    public void 破坑而出结束(Event ev, ScriptAccessory sa)
    {
        // 最后一个出土
        if (!_enable) return;
        if (_bsp.F50A_frameWorkAction == "") return;
        sa.Method.UnregistFrameworkUpdateAction(_bsp.F50A_frameWorkAction);
        _bsp.Reset(sa, 50);
        MoveStop(sa);
        SwitchAiMode(sa, true);
        sa.DebugMsg($"破坑而出结束，开启BMR", Debugging);
    }
    
    #endregion 得到宽恕的沙坑

    #region F60 仙人掌

    [ScriptMethod(name: "———————— 《F60 仙人掌》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void F60_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "分株读条计数", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44857"],
        userControl: true)]
    public void 分株读条计数(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        var center = new Vector3(-600, 0, -300);
        MoveTo(sa, center);
        if (_bsp.F60A_castCountOdd)
            // 若前一次是奇数次，则本次是偶数次，重置值，重置自带置False
            _bsp.Reset(sa, 60);
        else
        {
            _bsp.F60A_castCountOdd = !_bsp.F60A_castCountOdd;
            SwitchAiMode(sa, !_bsp.F60A_castCountOdd);
        }
        sa.DebugMsg($"四列式分株：{_bsp.F60A_castCountOdd}", Debugging);
    }
    
    [ScriptMethod(name: "仙人花路径点", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:18912"],
        userControl: true)]
    public void 仙人花路径点(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        if (!_bsp.F60A_castCountOdd) return;
        if (_bsp.F60A_sourcePosIdxVal > 10_00_00) return;
        // 以左上为(1,1)，先行后列
        
        var pos = ev.SourcePosition;
        var colIdx = (int)((pos.X + 626) / 10);
        var rowIdx = (int)((pos.Z + 326) / 10);
        var digitIdx = _bsp.F60A_sourcePosIdxVal switch
        {
            < 10 => 0,
            < 10_00 => 2,
            _ => 4
        };
        _bsp.F60A_sourcePosIdxVal += (int)(colIdx * Math.Pow(10, digitIdx) + rowIdx * Math.Pow(10, digitIdx + 1));
        sa.DebugMsg($"F60A_sourcePosIdxVal : {_bsp.F60A_sourcePosIdxVal}", Debugging);
        // 计算安全区
        if (digitIdx != 4) return;
        // 检测1与3是否在同行，若同行则处于情况一，不同行情况二
        var sourcePosIdxVal = _bsp.F60A_sourcePosIdxVal;
        var oneThreeSameRow = sourcePosIdxVal.GetDecimalDigit(1) == sourcePosIdxVal.GetDecimalDigit(5);
        
        // 获得起点
        var startCol = sourcePosIdxVal.GetDecimalDigit(4);
        var startRow = sourcePosIdxVal.GetDecimalDigit(5);
        if (oneThreeSameRow) 
            startRow = startRow == 2 ? 1 : 4;
        _bsp.F60A_safePosRouteIdxVal += startCol + startRow * 10;
        
        // 获得终点
        var destCol = sourcePosIdxVal.GetDecimalDigit(0);
        var destRow = sourcePosIdxVal.GetDecimalDigit(1);
        if (oneThreeSameRow) 
            destRow = destRow == 2 ? 1 : 4;
        _bsp.F60A_safePosRouteIdxVal += destCol * 100 + destRow * 1000;
        
        sa.DebugMsg($"仙人掌记录：{_bsp.F60A_sourcePosIdxVal}，安全路径记录：{_bsp.F60A_safePosRouteIdxVal}", Debugging);
        // 直接移动到起跑点

        var startPosX = startCol * 10 - 625f + (destCol < startCol ? -4.5f : 4.5f);
        var startPosZ = startRow * 10 - 325f + (destRow < startRow ? -4.5f : 4.5f);
        var startPos = new Vector3(startPosX, 0, startPosZ);
        // sa.DrawGuidance(startPos, 0, 4000, $"起跑点");
        MoveTo(sa, startPos);
    }

    [ScriptMethod(name: "仙人花开跑", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:44859", "TargetIndex:1"],
        userControl: true, suppress: 20000)]
    public void 仙人花开跑(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        var startCol = _bsp.F60A_safePosRouteIdxVal.GetDecimalDigit(0);
        var startRow = _bsp.F60A_safePosRouteIdxVal.GetDecimalDigit(1);
        var destCol = _bsp.F60A_safePosRouteIdxVal.GetDecimalDigit(2);
        var destRow = _bsp.F60A_safePosRouteIdxVal.GetDecimalDigit(3);
        
        var destPosX = destCol * 10 - 625f + (destCol < startCol ? 4.5f : -4.5f);
        var destPosZ = destRow * 10 - 325f + (destRow < startRow ? 4.5f : -4.5f);
        var destPos = new Vector3(destPosX, 0, destPosZ);
        _bsp.F60A_frameWorkAction = sa.Method.RegistFrameworkUpdateAction(Action);

        void Action()
        {
            var myPos = sa.Data.MyObject.Position;
            // 先横走，再竖走

            if (_bsp.F60A_routeState == 0 && Math.Abs(myPos.X - destPos.X) > 0.5f)
            {
                // sa.DrawGuidance(new Vector3(destPos.X, 0, myPos.Z), 0, 4000, $"横走");
                MoveTo(sa, new Vector3(destPos.X, 0, myPos.Z));
                _bsp.F60A_routeState = 1;
            }
            else if (_bsp.F60A_routeState == 1 && Math.Abs(myPos.X - destPos.X) <= 0.5f && Math.Abs(myPos.Z - destPos.Z) > 0.5f)
            {
                // sa.DrawGuidance(destPos, 0, 4000, $"竖走");
                MoveTo(sa, destPos);
                _bsp.F60A_routeState = 2;
            }
        }
    }

    [ScriptMethod(name: "飞针射击停止仙人花机制移动", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44866"], userControl: true)]
    public void 飞针射击停止仙人花机制移动(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        if (_bsp.F60A_frameWorkAction == "") return;
        sa.Method.UnregistFrameworkUpdateAction(_bsp.F60A_frameWorkAction);
        MoveStop(sa);
        SwitchAiMode(sa, true);
        sa.DebugMsg($"飞针射击开始，仙人花结束，开启BMR", Debugging);
    }

    #endregion F60 仙人掌

    #region F99 卓异的悲寂

    [ScriptMethod(name: "———————— 《F99 卓异的悲寂》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void F99_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "初始化设置Buff与血量差", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:44094"],
        userControl: Debugging)]
    public void 初始化设置f99(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        // 18666 暗男人
        // 18667 光女人
        if (_bsp.F99_DarkManObj != null && _bsp.F99_LightWomanObj != null) return;
        _bsp.F99_DarkManObj = sa.GetByDataId(18666).FirstOrDefault() ?? throw new InvalidOperationException();
        _bsp.F99_LightWomanObj = sa.GetByDataId(18667).FirstOrDefault() ?? throw new InvalidOperationException();
        sa.DebugMsg($"获得男女人ID：0x{_bsp.F99_DarkManObj.GameObjectId:x8}, 0x{_bsp.F99_LightWomanObj.GameObjectId:x8}", Debugging);

        _bsp.F99_DarkManHpMax = ((IBattleChara)_bsp.F99_DarkManObj).MaxHp;
        _bsp.F99_LightWomanHpMax = ((IBattleChara)_bsp.F99_LightWomanObj).MaxHp;
        sa.DebugMsg($"获得男女人HP：{_bsp.F99_DarkManHpMax}, {_bsp.F99_LightWomanHpMax}", Debugging);
        
        _bsp.F99_setTargetableFrameWorkAction = sa.Method.RegistFrameworkUpdateAction(ActionTarget);
        _bsp.F99_setHpDiffFrameWorkAction = sa.Method.RegistFrameworkUpdateAction(ActionHpDiff);
        _bsp.F99_changeBuffFrameWorkAction = sa.Method.RegistFrameworkUpdateAction(ActionChangeBuff);

        void ActionTarget()
        {
            try
            {
                // 4559 暗buff 1，4560 光Buff 2
                if (!sa.Data.MyObject.IsValid()) return;
                var myStatusState = (sa.Data.MyObject.HasStatus(4559) ? 1 : 0) + (sa.Data.MyObject.HasStatus(4560) ? 2 : 0);
                if (myStatusState == 0) return;
                if (myStatusState == _bsp.F99_buffState) return;

                sa.SetTargetable(_bsp.F99_DarkManObj, myStatusState != 1);
                sa.SetTargetable(_bsp.F99_LightWomanObj, myStatusState != 2);
                _bsp.F99_buffState = myStatusState;
            }
            catch (Exception e)
            {
                sa.DebugMsg($"{e}");
            }

        }

        void ActionHpDiff()
        {
            try
            {
                if (_bsp.F99_DarkManObj == null) return;
                if (!_bsp.F99_DarkManObj.IsValid()) return;
                if (_bsp.F99_DarkManObj is not IBattleChara darkMan) return;
                if (_bsp.F99_LightWomanObj is not IBattleChara lightWoman) return;
            
                var hpPercentMan = (float)darkMan.CurrentHp / _bsp.F99_DarkManHpMax;
                var hpPercentWoman = (float)lightWoman.CurrentHp / _bsp.F99_LightWomanHpMax;

                // 倾向于每5%进行一次更换
                if (hpPercentMan - hpPercentWoman > 0.05f)
                    _bsp.F99_nextBuffState = 2;
                else if (hpPercentMan - hpPercentWoman < -0.05f)
                    _bsp.F99_nextBuffState = 1;
            }
            catch (Exception e)
            {
                sa.DebugMsg($"{e}");
            }
        }

        void ActionChangeBuff()
        {
            try
            {
                if (!_bsp.F99_buffStateExchangeEnable) return;
                if (_bsp.F99_nextBuffState == _bsp.F99_buffState)
                {
                    _bsp.F99_changeBuffIdle = true;
                    return;
                }
                if (!_bsp.F99_changeBuffIdle) return;
                var myPos = sa.Data.MyObject.Position;
                _bsp.F99_changeBuffIdle = false;
                var minDistance = 50f;
                Vector3 minDistanceSwamp = new Vector3(0, 0, 0);
                foreach (var swamp in _bsp.F99_nextBuffState == 1 ? _bsp.F99_darkSwamps : _bsp.F99_lightSwamps)
                {
                    var distance = Vector3.Distance(myPos, swamp);
                    if (!(distance < minDistance)) continue;
                    minDistance = distance;
                    minDistanceSwamp = swamp;
                }
                // sa.DrawGuidance(minDistanceSwamp, 0, 4000, $"切换buff目标沼泽");
                MoveTo(sa, minDistanceSwamp);
            }
            catch (Exception e)
            {
                sa.DebugMsg($"{e}");
            }
        }
    }
    
    [ScriptMethod(name: "棘刺尾就位", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:44085"],
        userControl: true)]
    public void 棘刺尾就位(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        SwitchAiMode(sa, false);
        SwitchChangeBuffAvailability(sa, false);
        var distance = sa.Data.MyObject.Position.X - -600;
        var pos = sa.Data.MyObject.Position.WithX(distance > 0 ? -593 : -607);
        // sa.DrawGuidance(pos, 0, 4000, $"就近走");
        MoveTo(sa, pos);
    }
    
    [ScriptMethod(name: "棘刺尾结束（允许BMR）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:45118"],
        userControl: true)]
    public void 棘刺尾结束(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        SwitchAiMode(sa, true);
    }
    
    [ScriptMethod(name: "棘刺尾彻底结束（允许切换buff）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:45118"],
        userControl: true)]
    public void 棘刺尾彻底结束(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        SwitchChangeBuffAvailability(sa, true);
    }
    
    [ScriptMethod(name: "热风旋风开始", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4406[1389])$"],
        userControl: true)]
    public void 热风旋风开始(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        SwitchChangeBuffAvailability(sa, false);
    }
    
    [ScriptMethod(name: "热风结束", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:4562"],
        userControl: true)]
    public void 热风结束(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        SwitchChangeBuffAvailability(sa, true);
    }
    
    [ScriptMethod(name: "旋风结束", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44062)$"],
        userControl: true, suppress: 2000)]
    public void 旋风结束(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        SwitchChangeBuffAvailability(sa, true);
    }
    
    [ScriptMethod(name: "濒死Buff锁定", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:4561"],
        userControl: true)]
    public void 濒死Buff锁定(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        sa.DebugMsg($"检测到濒死Buff，锁定攻击目标", Debugging);
        _bsp.F99_nextBuffState = ev.TargetId == _bsp.F99_DarkManObj.GameObjectId ? 1 : 2;
        sa.Method.UnregistFrameworkUpdateAction(_bsp.F99_setHpDiffFrameWorkAction);
    }
    
    [ScriptMethod(name: "捕捉地火施法", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4407[45])$"],
        userControl: Debugging)]
    public void 捕捉地火施法(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        _bsp.Reset(sa, 99);
        
        const uint VERTICAL_FIRST = 44075;
        var center = new Vector3(-600, 0, -300);
        var pos = new Vector3(-606f, 0, -314f);
        _bsp.F99A_exaflareSafePos = ev.ActionId == VERTICAL_FIRST ? pos.PointCenterSymmetry(center) : pos;
    }
    
    [ScriptMethod(name: "捕捉地火源", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:44078"],
        userControl: Debugging)]
    public void 捕捉地火源(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        if (_bsp.F99A_exaflareDone) return;
        var center = new Vector3(-600, 0, -300);
        lock (_bsp)
        {
            var distance = Math.Abs(ev.EffectPosition.Z - -300);
            if (distance >= 1.5f) return;
            
            if (ev.EffectPosition.X > center.X)
                _bsp.F99A_exaflareSafePos = _bsp.F99A_exaflareSafePos.FoldPointHorizon(center.X);
            sa.DebugMsg($"获得地火安全区：{(_bsp.F99A_exaflareSafePos.X < center.X ? "左" : "右")}{(_bsp.F99A_exaflareSafePos.Z < center.Z ? "上" : "下")}", Debugging);
            _bsp.F99A_exaflareDone = true;
        }
    }
    
    [ScriptMethod(name: "移动至地火安全区", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:44079"],
        userControl: true, suppress: 10000)]
    public void 地火安全区(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        SwitchChangeBuffAvailability(sa, false);
        // sa.DrawGuidance(_bsp.F99A_exaflareSafePos, 0, 4000, $"地火安全区");
        MoveTo(sa, _bsp.F99A_exaflareSafePos);
        sa.DebugMsg($"去地火安全区", Debugging);
        _ = Task.Run(async () =>
        {
            await Task.Delay(13000);
            SwitchChangeBuffAvailability(sa, true);
        });
    }
    
    [ScriptMethod(name: "净罪之环禁止切换Buff", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:44082", "TargetIndex:1"],
        userControl: true)]
    public void 净罪之环禁止切换(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        SwitchChangeBuffAvailability(sa, false);
        _ = Task.Run(async () =>
        {
            await Task.Delay(8000);
            SwitchChangeBuffAvailability(sa, true);
        });
    }
    
    [ScriptMethod(name: "以太吸取禁用血量检测更换Buff", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4409[02])$"],
        userControl: true)]
    public void 以太吸取禁用血量检测(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        SwitchChangeBuffAvailability(sa, false);
    }
    
    [ScriptMethod(name: "以太吸取开启血量检测更换Buff", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44089|44092)$"],
        userControl: true)]
    public void 以太吸取开启血量检测(Event ev, ScriptAccessory sa)
    {
        if (!_enable) return;
        // 长读条读完后开启
        SwitchChangeBuffAvailability(sa, true);
    }

    #endregion F99 卓异的悲寂
    
    #region 脚本专用函数
    
    private void SetSpeed(ScriptAccessory sa, float speed)
    {
        sa.Method.SendChat($"/pdrspeed {speed}");
        sa.Method.SendChat($"/i-ching-commander speed {speed - 1f}");
        if (speed == 1f)
            sa.Method.SendChat($"/i-ching-commander speed dispose");
    }
    private void SwitchAiMode(ScriptAccessory sa, bool enable) => sa.Method.SendChat($"/bmrai {(enable ? "on" : "off")}");
    private void SwitchAntiKnockback(ScriptAccessory sa, bool enable) => sa.Method.SendChat($"/i-ching-commander anti_knock {(enable ? "0 0" : "dispose")}");
    private void MoveTo(ScriptAccessory sa, Vector3 point) => sa.Method.SendChat($"/vnav moveto {point.X} {point.Y} {point.Z}");
    private void MoveStop(ScriptAccessory sa) => sa.Method.SendChat($"/vnav stop");
    private void SwitchChangeBuffAvailability(ScriptAccessory sa, bool enable)
    {
        if (!enable)
        {
            _bsp.F99_buffStateExchangeEnable = false;
            if (!_bsp.F99_changeBuffIdle)
            {
                sa.DebugMsg($"停止自动切换Buff寻路", Debugging);
                MoveStop(sa);
            }
            _bsp.F99_changeBuffIdle = true;
            sa.DebugMsg($"禁止自动切换Buff", Debugging);
        }
        else
        {
            _bsp.F99_buffStateExchangeEnable = true;
            sa.DebugMsg($"允许自动切换Buff", Debugging);
        }
    }
    
    #endregion 脚本专用函数
    
    #region 参数容器类
    
    private class BossStateParams
    {
        // 百花齐放
        public int F10A_castRegionVal = 0;
        public int F10A_castCount = 0;
        // 压花
        public int F10B_castRegionVal = 0;
        public int F10B_castCount = 0;
        
        // 空降
        public int F20A_castRegionVal = 0;
        public int F20A_castCount = 0;
        
        // 落光
        public string F30A_frameWorkAction = "";
        public int F30A_playerTargetRegion = -1;
        
        // 进沙坑
        public Vector3[] F50A_safeSpots =
        [
            new Vector3(-293.90f, 0.23f, -310.39f),
            new Vector3(-288.05f, 0.26f, -300.83f),
            new Vector3(-293.08f, 0.25f, -293.05f),
            new Vector3(-302.96f, 0.16f, -290.20f),
            new Vector3(-307.92f, 0.19f, -298.70f),
            new Vector3(-308.81f, 0.16f, -307.84f),
        ];
        public int F50A_targetSpotIdx = -1;
        public string F50A_frameWorkAction = "";
        public int F50A_spotCount = 0;
        public bool F50A_keepRunning = false;
        
        // 分株仙人掌
        public bool F60A_castCountOdd = false;
        public int F60A_sourcePosIdxVal = 0;
        public int F60A_safePosRouteIdxVal = 0;
        public int F60A_routeState = 0;
        public string F60A_frameWorkAction = "";
        
        // 卓异的悲寂
        public IGameObject? F99_DarkManObj = null;
        public IGameObject? F99_LightWomanObj = null;
        public uint F99_DarkManHpMax = 0;
        public uint F99_LightWomanHpMax = 0;
        public string F99_setTargetableFrameWorkAction = "";
        public int F99_buffState = 0;
        
        public string F99_setHpDiffFrameWorkAction = "";
        public bool F99_buffStateExchangeEnable = true;
        public int F99_nextBuffState = 2;
        public string F99_changeBuffFrameWorkAction = "";
        public bool F99_changeBuffIdle = true;
        
        public readonly Vector3[] F99_lightSwamps =
        [
            new(-587.75f, 0, -309.53f), new(-610.48f, 0, -303.28f), 
            new(-596.48f, 0, -303.28f), new(-580.57f, 0, -292.26f),
            new(-604.45f, 0, -287.33f)
        ];
    
        public readonly Vector3[] F99_darkSwamps =
        [
            new(-595.81f, 0, -312.70f), new(-619f, 0, -307.57f), 
            new(-603.7f, 0, -296.5f), new(-589.53f, 0, -296.42f),
            new(-611.94f, 0, -290.33f)
        ];
        
        public Vector3 F99A_exaflareSafePos = new Vector3(-606f, 0, -314f);
        public bool F99A_exaflareDone = false;
        
        public void Reset(ScriptAccessory sa, int floor)
        {
            switch (floor)
            {
                case 10:
                    F10A_castRegionVal = 0;
                    F10A_castCount = 0;
                    F10B_castRegionVal = 0;
                    F10B_castCount = 0;
                    break;
                case 20:
                    F20A_castRegionVal = 0;
                    F20A_castCount = 0;
                    break;
                case 30:
                    F30A_frameWorkAction = "";
                    F30A_playerTargetRegion = -1;
                    break;
                case 50:
                    F50A_targetSpotIdx = -1;
                    F50A_frameWorkAction = "";
                    F50A_spotCount = 0;
                    F50A_keepRunning = false;
                    break;
                case 60:
                    F60A_castCountOdd = false;
                    F60A_sourcePosIdxVal = 0;
                    F60A_safePosRouteIdxVal = 0;
                    F60A_routeState = 0;
                    F60A_frameWorkAction = "";
                    break;
                case 99:
                    F99A_exaflareDone = false;
                    break;
                default:
                    break;
            }
            
            sa.DebugMsg($"F{floor} 参数被重置", Debugging);
        }
    }
    
    #endregion
}

#region 函数集

public static class IbcHelper
{
    public static IGameObject? GetById(this ScriptAccessory sa, ulong gameObjectId)
    {
        return sa.Data.Objects.SearchById(gameObjectId);
    }
    
    public static IEnumerable<IGameObject?> GetByDataId(this ScriptAccessory sa, uint dataId)
    {
        return sa.Data.Objects.Where(x => x.DataId == dataId);
    }
}
#region 计算函数

public static class MathTools
{
    public static float DegToRad(this float deg) => (deg + 360f) % 360f / 180f * float.Pi;
    public static float RadToDeg(this float rad) => (rad + 2 * float.Pi) % (2 * float.Pi) / float.Pi * 180f;
    
    /// <summary>
    /// 获得任意点与中心点的弧度值，以(0, 0, 1)方向为0，以(1, 0, 0)方向为pi/2。
    /// 即，逆时针方向增加。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static float GetRadian(this Vector3 point, Vector3 center)
        => MathF.Atan2(point.X - center.X, point.Z - center.Z);

    /// <summary>
    /// 获得任意点与中心点的长度。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static float GetLength(this Vector3 point, Vector3 center)
        => new Vector2(point.X - center.X, point.Z - center.Z).Length();
    
    /// <summary>
    /// 将任意点以中心点为圆心，逆时针旋转并延长。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <param name="radian">旋转弧度</param>
    /// <param name="length">基于该点延伸长度</param>
    /// <returns></returns>
    public static Vector3 RotateAndExtend(this Vector3 point, Vector3 center, float radian, float length = 0)
    {
        var baseRad = point.GetRadian(center);
        var baseLength = point.GetLength(center);
        var rotRad = baseRad + radian;
        return new Vector3(
            center.X + MathF.Sin(rotRad) * (length + baseLength),
            center.Y,
            center.Z + MathF.Cos(rotRad) * (length + baseLength)
        );
    }
    
    /// <summary>
    /// 获得某角度所在划分区域
    /// </summary>
    /// <param name="radian">输入弧度</param>
    /// <param name="regionNum">区域划分数量</param>
    /// <param name="baseRegionIdx">0度所在区域的初始Idx</param>>
    /// <param name="isDiagDiv">是否为斜分割，默认为false</param>
    /// <param name="isCw">是否顺时针增加，默认为false</param>
    /// <returns></returns>
    public static int RadianToRegion(this float radian, int regionNum, int baseRegionIdx = 0, bool isDiagDiv = false, bool isCw = false)
    {
        var sepRad = float.Pi * 2 / regionNum;
        var inputAngle = radian * (isCw ? -1 : 1) + (isDiagDiv ? sepRad / 2 : 0);
        var rad = (inputAngle + 4 * float.Pi) % (2 * float.Pi);
        return ((int)Math.Floor(rad / sepRad) + baseRegionIdx + regionNum) % regionNum;
    }
    
    /// <summary>
    /// 将输入点左右折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerX">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointHorizon(this Vector3 point, float centerX)
        => point with { X = 2 * centerX - point.X };

    /// <summary>
    /// 将输入点上下折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerZ">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointVertical(this Vector3 point, float centerZ)
        => point with { Z = 2 * centerZ - point.Z };

    /// <summary>
    /// 将输入点中心对称
    /// </summary>
    /// <param name="point">输入点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static Vector3 PointCenterSymmetry(this Vector3 point, Vector3 center) 
        => point.RotateAndExtend(center, float.Pi, 0);
    
    /// <summary>
    /// 获取给定整数的指定位数
    /// </summary>
    /// <param name="val">给定数值</param>
    /// <param name="x">对应位数，个位为0</param>
    /// <returns>返回指定位的数字，如果x超出范围返回0</returns>
    public static int GetDecimalDigit(this int val, int x)
        => (int)(Math.Abs(val) / Math.Pow(10, x) % 10);
    
    /// <summary>
    /// 获取整数的指定二进制位值
    /// </summary>
    /// <param name="val">给定数值</param>
    /// <param name="bitPosition">二进制位位置，从最低位开始，最低位为0</param>
    /// <returns>返回指定位的值：0 或 1</returns>
    public static int GetBinaryBit(this int val, int bitPosition)
        => (val >> bitPosition) & 1;
    
}

#endregion 计算函数

#region 绘图函数

public static class DrawTools
{
    /// <summary>
    /// 返回绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">绘图基准，可为UID或位置</param>
    /// <param name="targetObj">绘图指向目标，可为UID或位置</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="radian">绘制图形弧度范围</param>
    /// <param name="rotation">绘制图形旋转弧度，以owner面前为基准，逆时针增加</param>
    /// <param name="width">绘制图形宽度，部分图形可保持与长度一致</param>
    /// <param name="length">绘制图形长度，部分图形可保持与宽度一致</param>
    /// <param name="innerWidth">绘制图形内宽，部分图形可保持与长度一致</param>
    /// <param name="innerLength">绘制图形内长，部分图形可保持与宽度一致</param>
    /// <param name="drawModeEnum">绘图方式</param>
    /// <param name="drawTypeEnum">绘图类型</param>
    /// <param name="isSafe">是否使用安全色</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="byY">动画效果随距离变更</param>
    /// <param name="draw">是否直接绘图</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawOwnerBase(this ScriptAccessory sa, 
        object ownerObj, object targetObj, int delay, int destroy, string name, 
        float radian, float rotation, float width, float length, float innerWidth, float innerLength,
        DrawModeEnum drawModeEnum, DrawTypeEnum drawTypeEnum, bool isSafe = false,
        bool byTime = false, bool byY = false, bool draw = true)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.InnerScale = new Vector2(innerWidth, innerLength);
        dp.Radian = radian;
        dp.Rotation = rotation;
        dp.Color = isSafe ? sa.Data.DefaultSafeColor: sa.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        dp.ScaleMode |= byY ? ScaleMode.YByDistance : ScaleMode.None;
        
        switch (ownerObj)
        {
            case uint u:
                dp.Owner = u;
                break;
            case ulong ul:
                dp.Owner = ul;
                break;
            case Vector3 spos:
                dp.Position = spos;
                break;
            default:
                throw new ArgumentException($"ownerObj {ownerObj} 的目标类型 {ownerObj.GetType()} 输入错误");
        }

        switch (targetObj)
        {
            case 0:
            case 0u:
                break;
            case uint u:
                dp.TargetObject = u;
                break;
            case ulong ul:
                dp.TargetObject = ul;
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos;
                break;
            default:
                throw new ArgumentException($"targetObj {targetObj} 的目标类型 {targetObj.GetType()} 输入错误");
        }
        
        if (draw)
            sa.Method.SendDraw(drawModeEnum, drawTypeEnum, dp);
        return dp;
    }

    /// <summary>
    /// 返回指路绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">出发点</param>
    /// <param name="targetObj">结束点</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">箭头旋转角度</param>
    /// <param name="width">箭头宽度</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name,
        float rotation = 0, float width = 1f, bool isSafe = true, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width,
            width, 0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Displacement, isSafe, false, true, draw);
    
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, float rotation = 0, float width = 1f, bool isSafe = true,
        bool draw = true)
        => sa.DrawGuidance((ulong)sa.Data.Me, targetObj, delay, destroy, name, rotation, width, isSafe, draw);
}

#endregion 绘图函数

#region 调试函数

public static class DebugFunction
{
    public static void DebugMsg(this ScriptAccessory sa, string msg, bool enable = true, bool showInChatBox = true)
    {
        if (!enable) return;
        sa.Log.Debug(msg);
        if (!showInChatBox) return;
        sa.Method.SendChat($"/e {msg}");
    }
}

#endregion 调试函数

#region 特殊函数

public static class SpecialFunction
{
    public static void SetTargetable(this ScriptAccessory sa, IGameObject? obj, bool targetable)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            if (targetable)
            {
                if (obj.IsDead || obj.IsTargetable) return;
                charaStruct->TargetableStatus |= ObjectTargetableFlags.IsTargetable;
            }
            else
            {
                if (!obj.IsTargetable) return;
                charaStruct->TargetableStatus &= ~ObjectTargetableFlags.IsTargetable;
            }
        }
        sa.Log.Debug($"SetTargetable {targetable} => {obj.Name} {obj}");
    }

}

#endregion 特殊函数

#endregion 函数集

