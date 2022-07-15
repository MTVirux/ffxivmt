<html>
    <form action="<?=base_url('home/search')?>" method="POST">
        <input name="item_id" id="item_name" type="text" placeholder="Item ID"></input>
        <button type="submit" value="submit"> Search </button>
    </form>
    <a href="<?=base_url('home/marketable')?>"><button>Show Marketable</button></a>
    <a href="<?=base_url('home/materias')?>"><button>Show Materias</button></a>
</html>