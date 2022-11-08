namespace TodoTelegramAssistant
{
    public class TodoController
    {
        private TodoDbContext _context;
        public TodoController()
        {
            _context = new TodoDbContext();
        }

        public void ChangeLocalization(long ownerId, string localization)
        {
            User user = _context.Users.Where(owner => owner.OwnerId == ownerId).First();

            if (localization == "ua" || localization == "us")
            {
                switch (localization)
                {
                    case "ua":
                        user.Localization = Localization.UA;
                        break;
                    case "us":
                        user.Localization = Localization.US;
                        break;
                }
            }
            else
            {
                user.Localization = user.Localization.Next();
            }

            _context.SaveChanges();
        }

        public Localization GetLocalization(long ownerId)
        {
            User user = _context.Users.Where(owner => owner.OwnerId == ownerId).First();

            return user.Localization;
        }

        public void AddUser(long ownerId)
        {
            var user = new User
            {
                OwnerId = ownerId,
                Localization = Localization.UA
            };
            _context.Users.Add(user);
            _context.SaveChanges();
        }

        public void AddTodo(long ownerId, string title)
        {
            var todo = new Todo
            {
                Owner = _context.Users.Where(owner=>owner.OwnerId == ownerId).First(),
                Title = title
            };
            _context.Todos.Add(todo);
            _context.SaveChanges();
        }

        public void DeleteTodo(long ownerId, int todoId)
        {
            _context.Todos.Remove(_context.Todos.Where(todo => todo.TodoId == todoId && todo.Owner.OwnerId == ownerId).First());
            _context.SaveChanges();
        }

        public IEnumerable<Todo> GetAllTodos(long ownerId)
        {
            return _context.Todos.Where(todo => todo.Owner.OwnerId == ownerId);
        }

        public void ModifyTodo(int todoId, string title)
        {
            var todo = _context.Todos.Where(todo => todo.TodoId == todoId).First();
            todo.Title = title;
            _context.SaveChanges();
        }
    }
}
