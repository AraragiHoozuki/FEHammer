## Ability
用語：

 - bitmask_stats(x): 我方四維： 通過 bitmask 來指定四維 2=atk，4=spd，8=def，16=res，30 即表示全部四維。前面加1表示敵人四維， 如 102 表示敵人 atk， 130表示敵人全部四維
 - index_stats(x): 我方單個四維，0-4 分別表示血攻速防抗，前面加 1 表示敵人四維，如 11 表示敵人攻擊

### 438:
timing: 8, 67 = 自己和敵方回合開始時

skill_params2:
 - 賦予自己和周圍友軍四維buff和狀態
 - hp: 1 + 表示四維的bitmask，31表示全部四維
 - atk: buff數值
 - spd,def,res: 賦予的buff id，沒有則填-1

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


 ## states
 - 