# E-shop – popis domény (varianta B, málo strukturovaná)

> Stejná pravidla jako varianta A, ale sepsaná volným textem tak, jak je typicky píše analytik do zadání. Obsahuje stejné záměrné rozpory s kódem.

## Zákazníci

Každý zákazník má jméno (nejvýše 200 znaků) a e-mail, na který mu chodí potvrzení. E-mail musí být platný a nesmí být moc dlouhý – počítejme s max. 256 znaky. Firmy mohou vyplnit IČO, to má vždy přesně osm číslic. Zákazník má dále adresu (ulice, město, PSČ); PSČ se zadává jako pět číslic bez mezery, ulice max 200 a město max 100 znaků.

Obchodník může zákazníkovi nastavit úvěrový limit. Záporný limit nedává smysl. Nově založený zákazník je aktivní a má limit nula. Zákazníka lze deaktivovat a znovu aktivovat.

## Produkty

Produkt se identifikuje kódem – velká písmena, číslice a pomlčka, maximálně 20 znaků – a kód se v e-shopu nesmí opakovat, a to ani při pozdější změně kódu. Dále má název (do 200 znaků), cenu, sazbu DPH (0, 12 nebo 21 %) a volitelně hmotnost v kg, která samozřejmě nemůže být záporná. Cena musí být kladná. Produkt, který se už neprodává, se deaktivuje.

## Objednávky

Objednávku zakládá zákazník; dostane číslo (max 20 znaků) a datum vytvoření. Do nové objednávky se přidávají položky – u každé se pamatuje produkt, jeho název v době objednání, jednotková cena (kladná) a množství od 1 do 999 kusů. Cena položky je jednotková cena krát množství, celková cena objednávky je součet položek. Jedna objednávka nemůže mít víc než 50 položek.

Dokud je objednávka nová, lze položky přidávat a odebírat; odebrat položku, která v objednávce není, nejde. Objednávka se pak potvrdí (prázdnou potvrdit nelze), expeduje se (odeslaná) a nakonec je doručená. Zákazník může objednávku zrušit, pokud ještě nebyla odeslána – musí uvést důvod (max 500 znaků). Doručenou ani zrušenou objednávku už nelze dál měnit.
