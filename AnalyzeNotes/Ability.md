## Ability
用語：

 - bitmask_stats(x): 我方四維： 通過 bitmask 來指定四維 2=atk，4=spd，8=def，16=res，30 即表示全部四維。前面加1表示敵人四維， 如 102 表示敵人 atk， 130表示敵人全部四維
 - index_stats(x): 我方單個四維，0-4 分別表示血攻速防抗，前面加 1 表示敵人四維，如 11 表示敵人攻擊
 - sp(1,2,3), skill_params 简称
### 415
范围内我方效果：
sp2:
 - hp: 战斗后hp回复
 - atk: ?
 - spd: bitmask_stats
 - def: 战斗中属性+
 - res: ?
sp3:
 - hp: skill_flags
 - spd: 抵消敌人  bitmask_stats 强化

### 427 炎击
skill_params2:
 - hp: 天脉类型
 - atk: 天脉持续回合
 - spd: 未知
 - def: 未知
skill_params3:
 - hp: 攻击前伤害
 - res: 未知

### 438:
timing: 8, 67 = 自己和敵方回合開始時

skill_params2:
 - 賦予自己和周圍友軍四維buff和狀態
 - hp: 1 + 表示四維的bitmask，31表示全部四維
 - atk: buff數值
 - spd,def,res: 賦予的buff id，沒有則填-1

### 451 神速追击
skill_params2:
 - hp: 额外效果 3= 对方先制攻击？
 - atk: 神速倍率 4080, 40100, 10040 等
 - spd: 条件要求?
 - def: 条件比较值修正
 - res: 指定额外效果
    - 1: 受到伤害降低 sp3.spd%
    - 2: skill_flags(sp3.hp)? 且追击时破减伤 sp3.spd%

### 502
skill_params1: 戰鬥自己或對手中四維變化
 - hp: AABB，變動數值爲 index_stats(AA) 的 BB%
 - atk:  bitmask(atk), 表示變化的四維對象（即：敵人、我方？哪幾種四維？）
 - spd: 在前面變動的基礎上再加上 spd
 - def: 變動值上限
 - res: 


skill_params2:
 - hp: ABBCC, 範圍內 A（=1表示敵人）戰鬥中 四維（BB） 減少CC
 - atk: 範圍內敵人戰鬥中發動同 skill.flags(atk) 的效果
 - spd: 範圍內敵人戰鬥中發動同 skill.flags1(spd) 的效果?
 - def: 敵人靜謐 stats(def)

skill_params3:
 - hp: 2076 表示附加狀態
 - atk: 狀態1
 - spd: 狀態2

## Sub Ability(sp3.hp)

### 2066
 - atk: AABB, add 2 states
 - spd: skill_flags
 - def: damage_cut/bonus


 ## states
 - 0: 重壓
 - 1: 慌亂
 - 3: 移動+1
 - 4: 強化互克
 - 5: 取消
 - 6: 可以移动到周围2格内我方相邻的格子
 - 14: 猛攻
 - 21: 迴避
 - 27: 识破追击
 - 28: 天驅之道
 - 30: 抵消敵人強化
 - 31: 神軍師（連帶）
 - 35: 傷害+強化合計
 - 38: 弱點暴露
 - 41: 無法被守護
 - 42: 暗鬥
 - 48: 混亂
 - 49: 不和
 - 51: 七色吶喊
 - 55: 抵消祈禱
 - 56: 奮激（正手四維+移動距離）
 - 62: 七色呢喃
 - 63: 奪取
 - 64: 戰果轉讓
 - 65: 凍結
 - 69: 被害妄想
 - 70: 移動+2
 - 76: 初击鼓动
 - 80: 龍咒
 - 81: 火焰紋章
 - 88: 真強化增幅
 - 90: 老师的引导（四维+5，双减伤，2倍sp，战斗后奥义-1）
 - 98: 蒼炎勇者（遠反，四維+5, 首擊減傷10, 奧義傷害+CD×4）